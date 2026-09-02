using AzureSuite.Infrastructure.Configuration;
using AzureSuite.Web.Components;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureSuiteKeyVault(
    builder.Environment,
    new Uri("https://kv-azsuite-dev-pumpkin.vault.azure.net/"),
    new Dictionary<string, string>
    {
        ["web-client-secret"] = "AzureAd:ClientSecret",
        ["app-insights-connection-string"] = "ApplicationInsights:ConnectionString"
    });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]);

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
