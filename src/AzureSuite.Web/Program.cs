using AzureSuite.Web.Components;
using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// In Development, skip straight to the Azure CLI credential (our az login session) -
// DefaultAzureCredential's full chain otherwise wastes 10-20s probing Managed Identity's
// instance metadata endpoint, which doesn't exist on a dev machine. Once deployed, the
// full chain (including Managed Identity) is used automatically, no code change needed.
TokenCredential keyVaultCredential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new DefaultAzureCredential();

// Explicit secret-name -> config-key mapping, matching the {resource}-{purpose}
// naming convention used across the Key Vault (e.g. "sql-admin-password"),
// rather than the double-hyphen "--" auto-mapping convention some libraries expect.
builder.Configuration.AddAzureKeyVault(
    new Uri("https://kv-azsuite-dev-pumpkin.vault.azure.net/"),
    keyVaultCredential,
    new AzureSuiteKeyVaultSecretManager());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The Web app is a client: it signs users in via Entra ID (OpenID Connect) and
// acquires tokens to call the API on their behalf - it never validates tokens itself
// (that's the API's job) and never stores user credentials.
// Must request the API scope up front, at sign-in time - Blazor Server can't redirect
// mid-circuit for interactive consent later, so if this isn't requested here, calling
// the API afterwards fails with MsalUiRequiredException instead of a clean challenge.
var messagesApiScopes = builder.Configuration.GetSection("MessagesApi:Scopes").Get<string[]>() ?? [];

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(messagesApiScopes)
    .AddDownstreamApi("MessagesApi", builder.Configuration.GetSection("MessagesApi"))
    .AddInMemoryTokenCaches();

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

builder.Services.AddAuthorization(options =>
{
    // Every page requires sign-in - this app has no anonymous/public content.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// Maps Key Vault secret names to configuration keys explicitly, following this
/// project's {resource}-{purpose} naming convention instead of relying on the
/// default "--" to ":" auto-mapping (which would force awkward secret names).
/// </summary>
public class AzureSuiteKeyVaultSecretManager : KeyVaultSecretManager
{
    private static readonly Dictionary<string, string> SecretToConfigKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["web-client-secret"] = "AzureAd:ClientSecret"
    };

    public override bool Load(SecretProperties secret) => SecretToConfigKey.ContainsKey(secret.Name);

    public override string GetKey(KeyVaultSecret secret) => SecretToConfigKey[secret.Name];
}
