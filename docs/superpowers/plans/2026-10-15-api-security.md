# API Security Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require Entra ID tokens with specific app roles on every business endpoint of Catalog.Api and Ingestion.Api, and add MSAL sign-in with role-aware UI to the Blazor app.

**Architecture:** App roles are defined on one API app registration, declared in Graph Bicep (`infra/identity/`) and deployed manually by the owner. A shared `AzureSuite.Security` library holds the role and policy names and is referenced by both APIs and the WASM app. The APIs validate JWTs with Microsoft.Identity.Web, deny by default, and opt endpoints into named policies. The SPA signs in with MSAL and copies the `roles` claim from the API access token into its user, for display only.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Microsoft.Identity.Web 4.16.1, Microsoft.Authentication.WebAssembly.Msal 10.0.12, Microsoft.AspNetCore.Authorization 10.0.12, Microsoft Graph Bicep extension v1.0 `1.0.0`, xUnit + FluentAssertions + bUnit 2.10.3.

**Spec:** `docs/superpowers/specs/2026-10-15-api-security-design.md`

## Global Constraints

- Warning-free build, nullable enabled, no `#pragma warning disable`.
- Block-scoped namespaces in new files (`namespace X { … }`), except where the file being edited already uses file-scoped namespaces; match the surrounding file.
- No top-level statements; XML doc comments on every new class.
- 1:1 test projects: `services/Shared/AzureSuite.Security` → `tests/AzureSuite.Security.Tests`, with mirrored folders and one test class per production class.
- Role values exactly: `Catalog.Reader`, `Catalog.Admin`, `Messages.Submit`. Policy names exactly: `CatalogRead`, `CatalogWrite`, `SubmitMessages`.
- App role ids: Catalog.Reader `d9abc6ed-af5a-45b4-ade8-786deaf412c0`, Catalog.Admin `7af834bf-b716-45f6-b286-ce6689f6b3a6`, Messages.Submit `0eccf6ce-9b0e-4502-964d-cb5e95fe9444`; `access_as_user` scope `9c7d0b32-c8f8-429b-a554-aa96cb0f8d97`.
- API identifier URI: `api://46b33134-1b34-42b4-9848-6b40016d2e22/messaginghub-api` (tenant-scoped form; a Bicep resource can't reference its own appId for `api://<appId>`).
- Tenant id `46b33134-1b34-42b4-9848-6b40016d2e22`; owner object id `dab4a8d0-a4ee-4bbe-bd82-a0b9f87c5aff`.
- Web origins: `https://delightful-sky-05bade503.2.azurestaticapps.net`, `https://localhost:7206`, `http://localhost:5147`.
- No secrets in the repo. Client ids, tenant id, audience and scope aren't secrets and are committed.
- Anonymous endpoints only: `/health`, `/health/verbose`, `/openapi/*`, Scalar.
- CI principal gets no Graph permissions; `infra/identity/**` never triggers `deploy-infra.yml`.

## Review Focus

- **CORS preflight from the SPA:** a browser `OPTIONS` with `Origin` and `Access-Control-Request-Method`, sent without a token, must get a 2xx with `Access-Control-Allow-Origin`. If it gets a 401, the SPA can't call the API at all. Pinned in Task 2.
- **Role claim on an unauthenticated identity:** `RequireRole` alone only checks `IsInRole`, so a principal that has a role claim but isn't authenticated would pass. Every policy also requires an authenticated user. Pinned in Task 1.
- **Producer token on Catalog:** a token with only `Messages.Submit` must get 403 on `POST /clients`, not 2xx. That's the shared-audience risk. Pinned in Task 3.
- **Reader on `/routes/lookup`:** this route sits beside `/routes/{id:guid}`, and moving endpoints into groups must not break its matching. A reader gets 200. Pinned in Task 3.
- **Signed-in user with no roles:** the UI must show an access-denied state, not a blank page or an endless "Signing in…". Pinned in Task 7.

---

## File Structure

| File | Responsibility |
|---|---|
| `services/Shared/AzureSuite.Security/AppRoles.cs` | Role value constants |
| `services/Shared/AzureSuite.Security/AppPolicies.cs` | Policy name constants |
| `services/Shared/AzureSuite.Security/AuthorizationOptionsExtensions.cs` | `AddAzureSuitePolicies()` |
| `tests/AzureSuite.Security.Tests/AuthorizationOptionsExtensionsTests.cs` | Policy allow/deny matrix |
| `services/Shared/AzureSuite.Observability/HealthChecks/HealthCheckEndpointExtensions.cs` | Health endpoints marked anonymous |
| `services/Ingestion/Ingestion.Api/Program.cs` | JWT auth, fallback policy, `SubmitMessages` on `POST /messages` |
| `services/Catalog/Catalog.Api/Program.cs` | JWT auth, fallback policy, read/write endpoint groups |
| `tests/{Ingestion,Catalog}.Api.Tests/TestAuthHandler.cs` | Test auth scheme driven by the `X-Test-Roles` header |
| `tests/{Ingestion,Catalog}.Api.Tests/*ApiFactory.cs` | Swap in the test scheme; `CreateClientWithRoles` |
| `tests/{Ingestion,Catalog}.Api.Tests/AuthorizationTests.cs` | 401/403/2xx/anonymous health (+ preflight) |
| `infra/identity/{bicepconfig.json,main.bicep,identity.dev.bicepparam}` | App registrations, roles, grants, assignments |
| `frontends/AzureSuite.Web.Blazor/Security/ApiAccessOptions.cs` | API scope + authorized URLs |
| `frontends/AzureSuite.Web.Blazor/Security/ApiAuthorizationMessageHandler.cs` | Attaches API tokens to the typed clients |
| `frontends/AzureSuite.Web.Blazor/Security/JwtRoles.cs` | Reads `roles` from a JWT payload |
| `frontends/AzureSuite.Web.Blazor/Security/ApiRolesClaimsPrincipalFactory.cs` | Adds the API roles to the SPA user |
| `frontends/AzureSuite.Web.Blazor/Pages/Authentication.razor` | MSAL callback page |
| `frontends/AzureSuite.Web.Blazor/Layout/RedirectToLogin.razor` | Sends unauthenticated users to sign-in |
| `frontends/AzureSuite.Web.Blazor/Layout/LoginDisplay.razor` | Name + sign-out in the nav |

---

### Task 1: AzureSuite.Security shared library

**Files:**
- Create: `services/Shared/AzureSuite.Security/AzureSuite.Security.csproj`
- Create: `services/Shared/AzureSuite.Security/AppRoles.cs`
- Create: `services/Shared/AzureSuite.Security/AppPolicies.cs`
- Create: `services/Shared/AzureSuite.Security/AuthorizationOptionsExtensions.cs`
- Create: `tests/AzureSuite.Security.Tests/AzureSuite.Security.Tests.csproj`
- Create: `tests/AzureSuite.Security.Tests/AuthorizationOptionsExtensionsTests.cs`
- Modify: `AzureSuite.slnx` (add both projects)

**Interfaces:**
- Produces: `AzureSuite.Security.AppRoles.{CatalogReader, CatalogAdmin, MessagesSubmit}` (const string), `AzureSuite.Security.AppPolicies.{CatalogRead, CatalogWrite, SubmitMessages}` (const string), `static void AddAzureSuitePolicies(this AuthorizationOptions options)`.

- [ ] **Step 1: Create the project files**

`services/Shared/AzureSuite.Security/AzureSuite.Security.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AzureSuite.Security</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Only Microsoft.AspNetCore.Authorization: it runs both server-side and in Blazor
         WebAssembly, so the APIs and the SPA share these definitions. Never add server-only
         packages (e.g. Microsoft.Identity.Web) here. -->
    <PackageReference Include="Microsoft.AspNetCore.Authorization" Version="10.0.12" />
  </ItemGroup>

</Project>
```

`tests/AzureSuite.Security.Tests/AzureSuite.Security.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.12" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\services\Shared\AzureSuite.Security\AzureSuite.Security.csproj" />
  </ItemGroup>

</Project>
```

In `AzureSuite.slnx`, add `<Project Path="services/Shared/AzureSuite.Security/AzureSuite.Security.csproj" />` inside the `/services/Shared/` folder and `<Project Path="tests/AzureSuite.Security.Tests/AzureSuite.Security.Tests.csproj" />` inside `/tests/`.

- [ ] **Step 2: Write the failing tests**

`tests/AzureSuite.Security.Tests/AuthorizationOptionsExtensionsTests.cs`:
```csharp
using System.Security.Claims;
using AzureSuite.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Security.Tests
{
    public class AuthorizationOptionsExtensionsTests
    {
        private readonly IAuthorizationService _authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(options => options.AddAzureSuitePolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        private static ClaimsPrincipal SignedInWith(params string[] roles) =>
            new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), authenticationType: "Test"));

        private async Task<bool> Allows(ClaimsPrincipal user, string policy) =>
            (await _authorization.AuthorizeAsync(user, policy)).Succeeded;

        [Theory]
        [InlineData(AppRoles.CatalogReader, true)]
        [InlineData(AppRoles.CatalogAdmin, true)]
        [InlineData(AppRoles.MessagesSubmit, false)]
        public async Task CatalogRead_AllowsReaderOrAdminOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.CatalogRead)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppRoles.CatalogAdmin, true)]
        [InlineData(AppRoles.CatalogReader, false)]
        [InlineData(AppRoles.MessagesSubmit, false)]
        public async Task CatalogWrite_AllowsAdminOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.CatalogWrite)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppRoles.MessagesSubmit, true)]
        [InlineData(AppRoles.CatalogAdmin, false)]
        public async Task SubmitMessages_AllowsSubmitterOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.SubmitMessages)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppPolicies.CatalogRead)]
        [InlineData(AppPolicies.CatalogWrite)]
        [InlineData(AppPolicies.SubmitMessages)]
        public async Task EveryPolicy_DeniesASignedInUserWithNoRoles(string policy)
        {
            (await Allows(SignedInWith(), policy)).Should().BeFalse();
        }

        [Theory]
        [InlineData(AppPolicies.CatalogRead, AppRoles.CatalogAdmin)]
        [InlineData(AppPolicies.CatalogWrite, AppRoles.CatalogAdmin)]
        [InlineData(AppPolicies.SubmitMessages, AppRoles.MessagesSubmit)]
        public async Task EveryPolicy_DeniesAnUnauthenticatedIdentityEvenWithTheRoleClaim(string policy, string role)
        {
            var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)]));

            (await Allows(unauthenticated, policy)).Should().BeFalse();
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/AzureSuite.Security.Tests`
Expected: build FAILS: `AppRoles`, `AppPolicies` and `AddAzureSuitePolicies` don't exist.

- [ ] **Step 4: Implement**

`services/Shared/AzureSuite.Security/AppRoles.cs`:
```csharp
namespace AzureSuite.Security
{
    /// <summary>Entra ID app role values defined on the MessagingHub-Api registration
    /// (infra/identity/main.bicep). They arrive in the access token's <c>roles</c> claim, both
    /// for signed-in users and for applications using client credentials.</summary>
    public static class AppRoles
    {
        public const string CatalogReader = "Catalog.Reader";
        public const string CatalogAdmin = "Catalog.Admin";
        public const string MessagesSubmit = "Messages.Submit";
    }
}
```

`services/Shared/AzureSuite.Security/AppPolicies.cs`:
```csharp
namespace AzureSuite.Security
{
    /// <summary>Named authorization policies shared by the APIs (enforcement) and the web app
    /// (deciding what to show).</summary>
    public static class AppPolicies
    {
        public const string CatalogRead = "CatalogRead";
        public const string CatalogWrite = "CatalogWrite";
        public const string SubmitMessages = "SubmitMessages";
    }
}
```

`services/Shared/AzureSuite.Security/AuthorizationOptionsExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;

namespace AzureSuite.Security
{
    /// <summary>Registers the <see cref="AppPolicies"/> on an <see cref="AuthorizationOptions"/>.</summary>
    public static class AuthorizationOptionsExtensions
    {
        public static void AddAzureSuitePolicies(this AuthorizationOptions options)
        {
            // RequireRole alone only checks IsInRole, which an unauthenticated identity carrying a
            // role claim would pass; requiring authentication too closes that gap.
            options.AddPolicy(AppPolicies.CatalogRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.CatalogReader, AppRoles.CatalogAdmin));

            options.AddPolicy(AppPolicies.CatalogWrite, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.CatalogAdmin));

            options.AddPolicy(AppPolicies.SubmitMessages, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.MessagesSubmit));
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AzureSuite.Security.Tests`
Expected: PASS, 14 tests.

- [ ] **Step 6: Commit**

```bash
git add services/Shared/AzureSuite.Security tests/AzureSuite.Security.Tests AzureSuite.slnx
git commit -m "Add AzureSuite.Security with shared roles and policies"
```

---

### Task 2: Ingestion.Api requires `Messages.Submit`

**Files:**
- Modify: `services/Shared/AzureSuite.Observability/HealthChecks/HealthCheckEndpointExtensions.cs:21-30`
- Modify: `services/Ingestion/Ingestion.Api/Ingestion.Api.csproj`
- Modify: `services/Ingestion/Ingestion.Api/Program.cs`
- Modify: `services/Ingestion/Ingestion.Api/appsettings.json`
- Create: `tests/Ingestion.Api.Tests/TestAuthHandler.cs`
- Modify: `tests/Ingestion.Api.Tests/IngestionApiFactory.cs`
- Modify: `tests/Ingestion.Api.Tests/MessagesEndpointTests.cs`
- Create: `tests/Ingestion.Api.Tests/AuthorizationTests.cs`

**Interfaces:**
- Consumes: `AppRoles`, `AppPolicies`, `AddAzureSuitePolicies()` (Task 1).
- Produces: `TestAuthHandler` (scheme name `TestAuthHandler.SchemeName = "Test"`, header `TestAuthHandler.RolesHeader = "X-Test-Roles"`); `IngestionApiFactory.CreateClientWithRoles(params string[] roles)` returning `HttpClient`. Task 3 repeats the same shapes for Catalog.

- [ ] **Step 1: Write the test auth handler**

`tests/Ingestion.Api.Tests/TestAuthHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ingestion.Api.Tests
{
    /// <summary>Stands in for JWT bearer validation in integration tests. A request carrying
    /// <see cref="RolesHeader"/> is authenticated with those comma-separated roles (an empty value
    /// means signed in with no roles); a request without it is anonymous, so it gets a 401.</summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var roles = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
```

- [ ] **Step 2: Update the factory**

In `tests/Ingestion.Api.Tests/IngestionApiFactory.cs`, add these usings:
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
```
Then replace `ConfigureWebHost` and add the helper:
```csharp
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessagePublisher>();
                services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();
            });

            // ConfigureTestServices runs after Program's registrations, so these defaults win over
            // the JWT bearer scheme Microsoft.Identity.Web registered; that scheme is never invoked.
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                });
            });
        }

        /// <summary>A client whose requests are authenticated with exactly these roles.</summary>
        public HttpClient CreateClientWithRoles(params string[] roles)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
            return client;
        }
```

- [ ] **Step 3: Write the failing authorization tests**

`tests/Ingestion.Api.Tests/AuthorizationTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Security;
using FluentAssertions;

namespace Ingestion.Api.Tests
{
    public class AuthorizationTests : IClassFixture<IngestionApiFactory>
    {
        private static readonly object ValidMessage = new { messageType = "pacs.008", version = "1.0", payload = "{}" };
        private readonly IngestionApiFactory _factory;

        public AuthorizationTests(IngestionApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostMessages_Anonymous_Returns401()
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogAdmin)]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData("")]
        public async Task PostMessages_WithoutSubmitRole_Returns403(string role)
        {
            var response = await _factory.CreateClientWithRoles(role).PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task PostMessages_WithSubmitRole_Returns201()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.MessagesSubmit).PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/verbose")]
        public async Task Health_Anonymous_Returns200(string path)
        {
            var response = await _factory.CreateClient().GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CorsPreflightFromTheWebApp_WithoutToken_IsAllowed()
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/messages");
            request.Headers.Add("Origin", "https://delightful-sky-05bade503.2.azurestaticapps.net");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

            var response = await _factory.CreateClient().SendAsync(request);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Headers.GetValues("Access-Control-Allow-Origin").Should()
                .Contain("https://delightful-sky-05bade503.2.azurestaticapps.net");
        }
    }
}
```

Add a project reference to `tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj`:
```xml
    <ProjectReference Include="..\..\services\Shared\AzureSuite.Security\AzureSuite.Security.csproj" />
```

In `tests/Ingestion.Api.Tests/MessagesEndpointTests.cs`, change the constructor body so that the existing tests act as a submitter:
```csharp
            _client = factory.CreateClientWithRoles(AzureSuite.Security.AppRoles.MessagesSubmit);
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/Ingestion.Api.Tests`
Expected: FAIL. `PostMessages_Anonymous_Returns401` gets 201 and the 403 cases get 201, because nothing is enforced yet.

- [ ] **Step 5: Mark the health endpoints anonymous**

In `HealthCheckEndpointExtensions.MapAzureSuiteHealthChecks`, chain `.AllowAnonymous()` onto both `MapGet` calls. Add `using Microsoft.AspNetCore.Authorization;` if the compiler asks for it; `AllowAnonymous` is in `Microsoft.AspNetCore.Builder`.
```csharp
        endpoints.MapGet(basePath, (HttpContext context, HealthCheckService healthCheckService) =>
            HandleAsync(context, healthCheckService, verbose: false))
            .AllowAnonymous();

        endpoints.MapGet($"{basePath}/verbose", (HttpContext context, HealthCheckService healthCheckService) =>
            HandleAsync(context, healthCheckService, verbose: true))
            .AllowAnonymous();
```
Then add one sentence to the class's XML summary: `Both endpoints are anonymous: services deny by default, and uptime probes carry no token.`

- [ ] **Step 6: Wire authentication into Ingestion.Api**

`services/Ingestion/Ingestion.Api/Ingestion.Api.csproj`: add
```xml
    <PackageReference Include="Microsoft.Identity.Web" Version="4.16.1" />
```
and a `ProjectReference` to `..\..\Shared\AzureSuite.Security\AzureSuite.Security.csproj`, next to the existing Observability reference.

`services/Ingestion/Ingestion.Api/appsettings.json`: add a top-level section. `ClientId` stays empty until Task 5 fills it in.
```json
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "46b33134-1b34-42b4-9848-6b40016d2e22",
    "ClientId": "",
    "Audience": "api://46b33134-1b34-42b4-9848-6b40016d2e22/messaginghub-api"
  }
```

`services/Ingestion/Ingestion.Api/Program.cs`: add the usings `AzureSuite.Security`, `Microsoft.AspNetCore.Authorization` and `Microsoft.Identity.Web`. After `builder.Services.AddHealthChecks();` add:
```csharp
            builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
            builder.Services.AddAuthorization(options =>
            {
                options.AddAzureSuitePolicies();
                // Deny by default: any endpoint that doesn't name a policy or AllowAnonymous
                // still needs a valid token.
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });
```
Replace the block from `app.UseCors();` down to (not including) `app.UseHttpsRedirection();` with:
```csharp
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi().AllowAnonymous();
                app.MapScalarApiReference().AllowAnonymous();
            }
```
Append `.RequireAuthorization(AppPolicies.SubmitMessages)` to the `app.MapPost("/messages", …)` call. It goes after the lambda's closing `})` and before the `;`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Ingestion.Api.Tests tests/AzureSuite.Observability.Tests`
Expected: PASS, including the existing `MessagesEndpointTests` and the health extension tests.

- [ ] **Step 8: Commit**

```bash
git add services/Shared/AzureSuite.Observability services/Ingestion/Ingestion.Api tests/Ingestion.Api.Tests
git commit -m "Require Messages.Submit on Ingestion.Api, deny by default"
```

---

### Task 3: Catalog.Api requires `Catalog.Reader` / `Catalog.Admin`

**Files:**
- Modify: `services/Catalog/Catalog.Api/Catalog.Api.csproj`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Modify: `services/Catalog/Catalog.Api/appsettings.json`
- Create: `tests/Catalog.Api.Tests/TestAuthHandler.cs`
- Modify: `tests/Catalog.Api.Tests/CatalogApiFactory.cs`
- Modify: `tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj`
- Modify: `tests/Catalog.Api.Tests/MessageTypesEndpointsTests.cs`
- Create: `tests/Catalog.Api.Tests/AuthorizationTests.cs`

**Interfaces:**
- Consumes: `AppRoles`, `AppPolicies`, `AddAzureSuitePolicies()` (Task 1); health endpoints already anonymous (Task 2).
- Produces: `CatalogApiFactory.CreateClientWithRoles(params string[] roles)`.

- [ ] **Step 1: Add the test auth handler and factory helper**

`tests/Catalog.Api.Tests/TestAuthHandler.cs` is identical to Task 2's file except for the namespace (`Catalog.Api.Tests`). It's copied rather than shared because test projects don't reference each other here.
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catalog.Api.Tests
{
    /// <summary>Stands in for JWT bearer validation in integration tests. A request carrying
    /// <see cref="RolesHeader"/> is authenticated with those comma-separated roles (an empty value
    /// means signed in with no roles); a request without it is anonymous, so it gets a 401.</summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var roles = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
```

In `tests/Catalog.Api.Tests/CatalogApiFactory.cs`, add the usings `Microsoft.AspNetCore.Authentication` and `Microsoft.AspNetCore.TestHost`. At the end of `ConfigureWebHost`, after the existing `builder.ConfigureServices(...)` call, add:
```csharp
            // ConfigureTestServices runs after Program's registrations, so these defaults win over
            // the JWT bearer scheme Microsoft.Identity.Web registered; that scheme is never invoked.
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                });
            });
```
and add this method to the class:
```csharp
        /// <summary>A client whose requests are authenticated with exactly these roles.</summary>
        public HttpClient CreateClientWithRoles(params string[] roles)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
            return client;
        }
```
Add a `ProjectReference` to `..\..\services\Shared\AzureSuite.Security\AzureSuite.Security.csproj` in `Catalog.Api.Tests.csproj`.

In `MessageTypesEndpointsTests`, change the constructor body so the existing tests act as an admin, who can both read and write:
```csharp
            _client = factory.CreateClientWithRoles(AzureSuite.Security.AppRoles.CatalogAdmin);
```

- [ ] **Step 2: Write the failing authorization tests**

`tests/Catalog.Api.Tests/AuthorizationTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Security;
using FluentAssertions;

namespace Catalog.Api.Tests
{
    public class AuthorizationTests : IClassFixture<CatalogApiFactory>
    {
        private readonly CatalogApiFactory _factory;

        public AuthorizationTests(CatalogApiFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/message-types")]
        [InlineData("/clients")]
        [InlineData("/routes")]
        public async Task Reads_Anonymous_Return401(string path)
        {
            var response = await _factory.CreateClient().GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData(AppRoles.CatalogAdmin)]
        public async Task Reads_WithReaderOrAdmin_Return200(string role)
        {
            var client = _factory.CreateClientWithRoles(role);

            (await client.GetAsync("/message-types")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/clients")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/routes")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RouteLookup_WithReader_Returns200()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogReader)
                .GetAsync($"/routes/lookup?clientId={Guid.NewGuid()}&messageType=pacs.008&version=1.0");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Validate_WithReader_IsNotForbidden()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogReader)
                .PostAsJsonAsync("/message-types/pacs.008/1.0/validate", new { payload = "{}" });

            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData(AppRoles.MessagesSubmit)]
        [InlineData("")]
        public async Task Writes_WithoutAdmin_Return403(string role)
        {
            var client = _factory.CreateClientWithRoles(role);

            (await client.PostAsJsonAsync("/clients", new { name = "Intruder" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostAsJsonAsync("/message-types", new { name = "x", version = "1.0", schemaDefinition = "{}" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.DeleteAsync($"/routes/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Writes_Anonymous_Return401()
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/clients", new { name = "Intruder" });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateClient_WithAdmin_Returns201()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogAdmin)
                .PostAsJsonAsync("/clients", new { name = $"Client {Guid.NewGuid()}" });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Health_Anonymous_IsNotUnauthorized()
        {
            // The catalog-populated check may report Unhealthy (503) on an empty test database;
            // the point here is only that no token is needed.
            var response = await _factory.CreateClient().GetAsync("/health?tags=db");

            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Catalog.Api.Tests`
Expected: FAIL. The anonymous cases return 200/201 instead of 401, and the 403 cases return 2xx.

- [ ] **Step 4: Wire authentication and group the endpoints**

`Catalog.Api.csproj`: add `<PackageReference Include="Microsoft.Identity.Web" Version="4.16.1" />` and a `ProjectReference` to `..\..\Shared\AzureSuite.Security\AzureSuite.Security.csproj`.

`appsettings.json`: add the same `AzureAd` section as Task 2, with `ClientId` empty.

`Program.cs`: add the usings `AzureSuite.Security`, `Microsoft.AspNetCore.Authorization` and `Microsoft.Identity.Web`. After the `AddHealthChecks()` chain, add the same `AddMicrosoftIdentityWebApiAuthentication` + `AddAuthorization` block as Task 2, Step 6. After `app.UseCors();` add `app.UseAuthentication(); app.UseAuthorization();`. Chain `.AllowAnonymous()` onto `app.MapOpenApi()` and `app.MapScalarApiReference()`.

Right after `app.MapAzureSuiteHealthChecks();`, create the two groups:
```csharp
            // Every Catalog endpoint is either a read (Reader or Admin) or a write (Admin only).
            var reads = app.MapGroup(string.Empty).RequireAuthorization(AppPolicies.CatalogRead);
            var writes = app.MapGroup(string.Empty).RequireAuthorization(AppPolicies.CatalogWrite);
```
Then change the receiver of each existing endpoint from `app.` to the right group, leaving the lambdas untouched:

| Endpoint | Receiver |
|---|---|
| `MapPost("/message-types", …)` | `writes` |
| `MapGet("/message-types/{name}/{version}", …)` | `reads` |
| `MapGet("/message-types", …)` | `reads` |
| `MapPost("/message-types/{name}/{version}/validate", …)` | `reads` |
| `MapPost("/clients", …)` | `writes` |
| `MapGet("/clients/{id:guid}", …)` | `reads` |
| `MapGet("/clients", …)` | `reads` |
| `MapPut("/clients/{id:guid}", …)` | `writes` |
| `MapDelete("/clients/{id:guid}", …)` | `writes` |
| `MapPost("/routes", …)` | `writes` |
| `MapGet("/routes/{id:guid}", …)` | `reads` |
| `MapGet("/routes", …)` | `reads` |
| `MapPut("/routes/{id:guid}", …)` | `writes` |
| `MapDelete("/routes/{id:guid}", …)` | `writes` |
| `MapGet("/routes/lookup", …)` | `reads` |

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Catalog.Api.Tests`
Expected: PASS, both the new `AuthorizationTests` and the existing `MessageTypesEndpointsTests`.

- [ ] **Step 6: Commit**

```bash
git add services/Catalog/Catalog.Api tests/Catalog.Api.Tests
git commit -m "Require Catalog roles on Catalog.Api, deny by default"
```

---

### Task 4: Identity infrastructure in Graph Bicep

**Files:**
- Create: `infra/identity/bicepconfig.json`
- Create: `infra/identity/main.bicep`
- Create: `infra/identity/identity.dev.bicepparam`
- Modify: `.github/workflows/deploy-infra.yml:4-8`

**Interfaces:**
- Produces deployment outputs: `tenantId`, `apiClientId`, `apiAudience`, `apiScope`, `webClientId`, `producerClientId`. Task 5 consumes them.

- [ ] **Step 1: Write the Bicep config**

`infra/identity/bicepconfig.json`:
```json
{
  "extensions": {
    "microsoftGraphV1": "br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1.0:1.0.0"
  }
}
```

- [ ] **Step 2: Write `infra/identity/main.bicep`**

```bicep
// Entra ID app registrations, app roles and assignments for the MessagingHub APIs.
// Deployed MANUALLY by the owner with their own az login (it needs Entra rights the CI
// principal deliberately doesn't have). Not referenced by ../main.bicep and excluded from
// deploy-infra.yml. Graph resources don't support what-if; re-running is idempotent
// (uniqueName keys) and fixes the occasional replication-delay failure on first run.
extension microsoftGraphV1

@description('Object id of the owner user, who receives every app role.')
param ownerPrincipalId string

@description('Origins of the web app; each gets <origin>/authentication/login-callback as a SPA redirect URI.')
param webOrigins array

// Tenant-scoped identifier URI: the api://<appId> form would need the app to reference itself.
var apiIdentifierUri = 'api://${tenant().tenantId}/messaginghub-api'
var accessAsUserScopeId = '9c7d0b32-c8f8-429b-a554-aa96cb0f8d97'
var roleIds = {
  catalogReader: 'd9abc6ed-af5a-45b4-ade8-786deaf412c0'
  catalogAdmin: '7af834bf-b716-45f6-b286-ce6689f6b3a6'
  messagesSubmit: '0eccf6ce-9b0e-4502-964d-cb5e95fe9444'
}
var microsoftGraphAppId = '00000003-0000-0000-c000-000000000000'
var graphOpenIdScopeId = '37f7f235-527c-4136-accd-4a02d197296e'
var graphProfileScopeId = '14dad69e-099b-42c9-810b-d002981feec1'
var graphOfflineAccessScopeId = '7427e0e9-2fba-42fe-b0c0-848c9e6a8182'

resource apiApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-api'
  displayName: 'MessagingHub-Api'
  signInAudience: 'AzureADMyOrg'
  identifierUris: [apiIdentifierUri]
  api: {
    requestedAccessTokenVersion: 2
    oauth2PermissionScopes: [
      {
        id: accessAsUserScopeId
        value: 'access_as_user'
        type: 'User'
        isEnabled: true
        adminConsentDisplayName: 'Access the MessagingHub APIs'
        adminConsentDescription: 'Lets the web app call the Catalog and Ingestion APIs as the signed-in user.'
        userConsentDisplayName: 'Access the MessagingHub APIs'
        userConsentDescription: 'Lets the web app call the Catalog and Ingestion APIs on your behalf.'
      }
    ]
  }
  appRoles: [
    {
      id: roleIds.catalogReader
      value: 'Catalog.Reader'
      displayName: 'Catalog reader'
      description: 'Read message types, clients and routes; validate payloads; look up routes.'
      allowedMemberTypes: ['User', 'Application']
      isEnabled: true
    }
    {
      id: roleIds.catalogAdmin
      value: 'Catalog.Admin'
      displayName: 'Catalog admin'
      description: 'Create, update and delete message types, clients and routes.'
      allowedMemberTypes: ['User']
      isEnabled: true
    }
    {
      id: roleIds.messagesSubmit
      value: 'Messages.Submit'
      displayName: 'Message submitter'
      description: 'Submit messages to the Ingestion API.'
      allowedMemberTypes: ['User', 'Application']
      isEnabled: true
    }
  ]
}

resource apiSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: apiApp.appId
}

resource webApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-web'
  displayName: 'MessagingHub-Web'
  signInAudience: 'AzureADMyOrg'
  spa: {
    redirectUris: [for origin in webOrigins: '${origin}/authentication/login-callback']
  }
  requiredResourceAccess: [
    {
      resourceAppId: apiApp.appId
      resourceAccess: [{ id: accessAsUserScopeId, type: 'Scope' }]
    }
    {
      resourceAppId: microsoftGraphAppId
      resourceAccess: [
        { id: graphOpenIdScopeId, type: 'Scope' }
        { id: graphProfileScopeId, type: 'Scope' }
        { id: graphOfflineAccessScopeId, type: 'Scope' }
      ]
    }
  ]
}

resource webSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: webApp.appId
}

resource microsoftGraphSp 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: microsoftGraphAppId
}

// Tenant-wide consent, so nobody gets a consent prompt on first sign-in.
resource webToApiGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webSp.id
  resourceId: apiSp.id
  consentType: 'AllPrincipals'
  scope: 'access_as_user'
}

resource webToGraphGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webSp.id
  resourceId: microsoftGraphSp.id
  consentType: 'AllPrincipals'
  scope: 'openid profile offline_access'
}

// Stand-in producer for testing Ingestion with client credentials. Its secret is created
// outside Bicep (az ad app credential reset), since Graph Bicep can't create passwords.
resource producerApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-testproducer'
  displayName: 'MessagingHub-TestProducer'
  signInAudience: 'AzureADMyOrg'
}

resource producerSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: producerApp.appId
}

resource ownerRoleAssignments 'Microsoft.Graph/appRoleAssignedTo@v1.0' = [for role in items(roleIds): {
  appRoleId: role.value
  principalId: ownerPrincipalId
  resourceId: apiSp.id
}]

resource producerSubmitAssignment 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  appRoleId: roleIds.messagesSubmit
  principalId: producerSp.id
  resourceId: apiSp.id
}

output tenantId string = tenant().tenantId
output apiClientId string = apiApp.appId
output apiAudience string = apiIdentifierUri
output apiScope string = '${apiIdentifierUri}/access_as_user'
output webClientId string = webApp.appId
output producerClientId string = producerApp.appId
```

`infra/identity/identity.dev.bicepparam`:
```bicep
using 'main.bicep'

param ownerPrincipalId = 'dab4a8d0-a4ee-4bbe-bd82-a0b9f87c5aff'
param webOrigins = [
  'https://delightful-sky-05bade503.2.azurestaticapps.net'
  'https://localhost:7206'
  'http://localhost:5147'
]
```

- [ ] **Step 3: Validate the Bicep**

Run: `az bicep build --file infra/identity/main.bicep --stdout > /dev/null && az bicep build-params --file infra/identity/identity.dev.bicepparam --stdout > /dev/null && echo OK`
Expected: `OK` with no warnings. The first run restores the extension from MCR. Fix any type errors the compiler reports against the Graph types. Property names come from the Graph v1.0 schema, and the compiler's suggestions are authoritative.

- [ ] **Step 4: Keep CI away from it**

In `.github/workflows/deploy-infra.yml`, change the `paths` filter. GitHub doesn't allow `paths` and `paths-ignore` on the same event, so use a negation pattern:
```yaml
  push:
    branches: [master]
    paths:
      - 'infra/**'
      - '!infra/identity/**'
```

- [ ] **Step 5: Commit**

```bash
git add infra/identity .github/workflows/deploy-infra.yml
git commit -m "Add Entra ID app registrations and roles as Graph Bicep"
```

---

### Task 5: CHECKPOINT: owner deploys identity, then fill in config

This task needs the owner's own Entra session. The agent prepares the commands, the owner runs the deployment, and the agent writes the outputs into config.

**Files:**
- Modify: `services/Ingestion/Ingestion.Api/appsettings.json` (`AzureAd:ClientId`)
- Modify: `services/Catalog/Catalog.Api/appsettings.json` (`AzureAd:ClientId`)

- [ ] **Step 1 (owner): Deploy**

```powershell
az deployment group create -g rg-messaginghub-dev `
  -f infra/identity/main.bicep -p infra/identity/identity.dev.bicepparam `
  --query properties.outputs
```
Expected: six outputs. If a role assignment fails with a "does not exist" or replication error, run the same command again.

- [ ] **Step 2 (owner): Create the test producer secret and keep it locally**

```powershell
az ad app credential reset --id <producerClientId> --display-name local-testing --years 1 --query password -o tsv
```
Store it in a password manager. It must never go into the repo or the chat.

- [ ] **Step 3: Write the API client id into both APIs' `appsettings.json`**

Set `AzureAd:ClientId` to the `apiClientId` output in both files. Run `dotnet test AzureSuite.slnx`. Expected: PASS, since the tests use the test scheme.

- [ ] **Step 4: Set the web variables in the `azure-dev` environment**

```bash
gh variable set AZURE_AD_TENANT_ID --env azure-dev --body "<tenantId>"
gh variable set AZURE_AD_WEB_CLIENT_ID --env azure-dev --body "<webClientId>"
gh variable set API_SCOPE --env azure-dev --body "<apiScope>"
```

- [ ] **Step 5: Commit**

```bash
git add services/Ingestion/Ingestion.Api/appsettings.json services/Catalog/Catalog.Api/appsettings.json
git commit -m "Configure API client id from the identity deployment"
```

---

### Task 6: Blazor authentication plumbing

**Files:**
- Modify: `frontends/AzureSuite.Web.Blazor/AzureSuite.Web.Blazor.csproj`
- Create: `frontends/AzureSuite.Web.Blazor/Security/ApiAccessOptions.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Security/ApiAuthorizationMessageHandler.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Security/JwtRoles.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Security/ApiRolesClaimsPrincipalFactory.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/Program.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/wwwroot/appsettings.json`
- Modify: `frontends/AzureSuite.Web.Blazor/wwwroot/appsettings.Development.json`
- Modify: `.github/workflows/build.yml` (`deploy-web` → "Substitute environment configuration")
- Create: `tests/AzureSuite.Web.Blazor.Tests/Security/JwtRolesTests.cs`
- Create: `tests/AzureSuite.Web.Blazor.Tests/Security/ApiRolesClaimsPrincipalFactoryTests.cs`

**Interfaces:**
- Consumes: `AddAzureSuitePolicies()` (Task 1); `webClientId`, `apiScope`, `tenantId` (Task 5).
- Produces: `ApiAccessOptions(string Scope, IReadOnlyList<Uri> AuthorizedUrls)` (singleton); `ApiAuthorizationMessageHandler`; `static IReadOnlyList<string> JwtRoles.Read(string jwt)`; `ApiRolesClaimsPrincipalFactory`. Task 7 relies on `AuthenticationStateProvider` and the policies being registered.

- [ ] **Step 1: Write the failing `JwtRoles` tests**

`tests/AzureSuite.Web.Blazor.Tests/Security/JwtRolesTests.cs`:
```csharp
using System.Text;
using AzureSuite.Web.Blazor.Security;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.Security
{
    public class JwtRolesTests
    {
        internal static string TokenWithPayload(string json)
        {
            static string Encode(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{Encode("{\"alg\":\"none\"}")}.{Encode(json)}.signature";
        }

        [Fact]
        public void Read_ReturnsEveryRoleInThePayload()
        {
            var token = TokenWithPayload("{\"roles\":[\"Catalog.Reader\",\"Messages.Submit\"]}");

            JwtRoles.Read(token).Should().Equal("Catalog.Reader", "Messages.Submit");
        }

        [Fact]
        public void Read_WithNoRolesClaim_ReturnsEmpty()
        {
            JwtRoles.Read(TokenWithPayload("{\"name\":\"Oscar\"}")).Should().BeEmpty();
        }

        [Fact]
        public void Read_HandlesPayloadsThatNeedBase64Padding()
        {
            // 1-char and 2-char remainders exercise both padding branches.
            JwtRoles.Read(TokenWithPayload("{\"roles\":[\"A\"]}")).Should().Equal("A");
            JwtRoles.Read(TokenWithPayload("{\"roles\":[\"AB\"]}")).Should().Equal("AB");
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-jwt")]
        [InlineData("a.%%%.c")]
        public void Read_WithAMalformedToken_ReturnsEmpty(string token)
        {
            JwtRoles.Read(token).Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter JwtRolesTests`
Expected: build FAILS: `JwtRoles` doesn't exist.

- [ ] **Step 3: Implement `JwtRoles`**

`frontends/AzureSuite.Web.Blazor/Security/JwtRoles.cs`:
```csharp
using System.Text;
using System.Text.Json;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>Reads the <c>roles</c> claim from a JWT's payload without validating it. Use it
    /// only to decide what the UI shows; the APIs validate the same token and enforce the roles.</summary>
    public static class JwtRoles
    {
        public static IReadOnlyList<string> Read(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                return [];
            }

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

                return document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array
                    ? roles.EnumerateArray().Select(r => r.GetString()).OfType<string>().ToList()
                    : [];
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                return [];
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter JwtRolesTests`
Expected: PASS, 6 tests.

- [ ] **Step 5: Add packages and the remaining security classes**

`AzureSuite.Web.Blazor.csproj`: add
```xml
    <PackageReference Include="Microsoft.Authentication.WebAssembly.Msal" Version="10.0.12" />
```
and `<ProjectReference Include="..\..\services\Shared\AzureSuite.Security\AzureSuite.Security.csproj" />`.

`frontends/AzureSuite.Web.Blazor/Security/ApiAccessOptions.cs`:
```csharp
namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>The delegated scope requested for the MessagingHub APIs, and the base URLs that
    /// may receive the resulting token. Tokens are never attached to any other host.</summary>
    public sealed record ApiAccessOptions(string Scope, IReadOnlyList<Uri> AuthorizedUrls);
}
```

`frontends/AzureSuite.Web.Blazor/Security/ApiAuthorizationMessageHandler.cs`:
```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>Attaches an API access token to requests sent to <see cref="ApiAccessOptions.AuthorizedUrls"/>.</summary>
    public sealed class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
    {
        public ApiAuthorizationMessageHandler(IAccessTokenProvider provider, NavigationManager navigation, ApiAccessOptions api)
            : base(provider, navigation)
        {
            ConfigureHandler(authorizedUrls: api.AuthorizedUrls.Select(u => u.ToString()), scopes: [api.Scope]);
        }
    }
}
```

`frontends/AzureSuite.Web.Blazor/Security/ApiRolesClaimsPrincipalFactory.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>App roles live on the API registration, so they're in the API access token, not in
    /// this app's ID token. This copies them onto the signed-in user so AuthorizeView and
    /// [Authorize(Policy)] work, using the same role and policy definitions as the APIs.</summary>
    public sealed class ApiRolesClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
    {
        private readonly ApiAccessOptions _api;

        public ApiRolesClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor, ApiAccessOptions api)
            : base(accessor)
        {
            _api = api;
        }

        public override async ValueTask<ClaimsPrincipal> CreateUserAsync(RemoteUserAccount account, RemoteAuthenticationUserOptions options)
        {
            var user = await base.CreateUserAsync(account, options);
            if (user.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
            {
                return user;
            }

            var result = await TokenProvider.RequestAccessToken(new AccessTokenRequestOptions { Scopes = [_api.Scope] });
            if (result.TryGetToken(out var token))
            {
                foreach (var role in JwtRoles.Read(token.Value))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
                }
            }

            return user;
        }
    }
}
```

- [ ] **Step 6: Write the factory test**

`tests/AzureSuite.Web.Blazor.Tests/Security/ApiRolesClaimsPrincipalFactoryTests.cs`:
```csharp
using AzureSuite.Web.Blazor.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace AzureSuite.Web.Blazor.Tests.Security
{
    public class ApiRolesClaimsPrincipalFactoryTests
    {
        private sealed class FakeTokenProvider : IAccessTokenProvider, IAccessTokenProviderAccessor
        {
            private readonly string? _token;
            public FakeTokenProvider(string? token) => _token = token;
            public IAccessTokenProvider TokenProvider => this;
            public string[] LastRequestedScopes { get; private set; } = [];

            public ValueTask<AccessTokenResult> RequestAccessToken() => RequestAccessToken(new AccessTokenRequestOptions());

            public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
            {
                LastRequestedScopes = options.Scopes?.ToArray() ?? [];
                var status = _token is null ? AccessTokenResultStatus.RequiresRedirect : AccessTokenResultStatus.Success;
                var token = new AccessToken { Value = _token ?? string.Empty, Expires = DateTimeOffset.UtcNow.AddHours(1), GrantedScopes = LastRequestedScopes };
                return ValueTask.FromResult(new AccessTokenResult(status, token, "authentication/login", null));
            }
        }

        private static readonly ApiAccessOptions Api = new("api://tenant/messaginghub-api/access_as_user", []);
        private static readonly RemoteAuthenticationUserOptions UserOptions = new() { AuthenticationType = "msal", NameClaim = "name" };
        private static RemoteUserAccount SignedInAccount() => new() { AdditionalProperties = new Dictionary<string, object> { ["name"] = "Oscar" } };

        [Fact]
        public async Task CreateUserAsync_AddsTheRolesFromTheApiAccessToken()
        {
            var provider = new FakeTokenProvider(JwtRolesTests.TokenWithPayload("{\"roles\":[\"Catalog.Admin\",\"Messages.Submit\"]}"));
            var factory = new ApiRolesClaimsPrincipalFactory(provider, Api);

            var user = await factory.CreateUserAsync(SignedInAccount(), UserOptions);

            user.IsInRole("Catalog.Admin").Should().BeTrue();
            user.IsInRole("Messages.Submit").Should().BeTrue();
            user.IsInRole("Catalog.Reader").Should().BeFalse();
            provider.LastRequestedScopes.Should().Equal(Api.Scope);
        }

        [Fact]
        public async Task CreateUserAsync_WhenNoTokenIsAvailable_ReturnsTheUserWithoutRoles()
        {
            var factory = new ApiRolesClaimsPrincipalFactory(new FakeTokenProvider(null), Api);

            var user = await factory.CreateUserAsync(SignedInAccount(), UserOptions);

            user.Identity!.IsAuthenticated.Should().BeTrue();
            user.IsInRole("Catalog.Admin").Should().BeFalse();
        }

        [Fact]
        public async Task CreateUserAsync_ForASignedOutAccount_DoesNotRequestAToken()
        {
            var provider = new FakeTokenProvider("unused");
            var factory = new ApiRolesClaimsPrincipalFactory(provider, Api);

            var user = await factory.CreateUserAsync(null!, UserOptions);

            user.Identity!.IsAuthenticated.Should().BeFalse();
            provider.LastRequestedScopes.Should().BeEmpty();
        }
    }
}
```
If `AccessTokenResult`'s constructor signature differs in 10.0.x, adjust the fake to whichever public constructor the compiler offers. The test's intent (success vs. redirect) is what matters.

- [ ] **Step 7: Run the factory tests**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter ApiRolesClaimsPrincipalFactoryTests`
Expected: PASS, 3 tests.

- [ ] **Step 8: Wire `Program.cs` and config**

`frontends/AzureSuite.Web.Blazor/Program.cs`: add the usings `AzureSuite.Security`, `AzureSuite.Web.Blazor.Security` and `Microsoft.AspNetCore.Components.WebAssembly.Authentication`. After the two `ApiEndpoint` variables are created, register access and auth. Then chain the handler onto both typed clients:
```csharp
            var apiAccess = new ApiAccessOptions(
                builder.Configuration["ApiScope"] ?? string.Empty,
                [catalogApi.BaseAddress, ingestionApi.BaseAddress]);
            builder.Services.AddSingleton(apiAccess);
            builder.Services.AddScoped<ApiAuthorizationMessageHandler>();

            builder.Services.AddMsalAuthentication<RemoteAuthenticationState, RemoteUserAccount>(options =>
                {
                    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
                    options.ProviderOptions.DefaultAccessTokenScopes.Add(apiAccess.Scope);
                    options.ProviderOptions.LoginMode = "redirect";
                    options.UserOptions.NameClaim = "name";
                })
                .AddAccountClaimsPrincipalFactory<RemoteAuthenticationState, RemoteUserAccount, ApiRolesClaimsPrincipalFactory>();
            builder.Services.AddAuthorizationCore(options => options.AddAzureSuitePolicies());
            builder.Services.AddCascadingAuthenticationState();
```
The two `AddHttpClient<…>(…)` calls move below this block and each gets `.AddHttpMessageHandler<ApiAuthorizationMessageHandler>()` chained on.

`wwwroot/appsettings.json`: add
```json
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/__AZURE_AD_TENANT_ID__",
    "ClientId": "__AZURE_AD_WEB_CLIENT_ID__",
    "ValidateAuthority": true
  },
  "ApiScope": "__API_SCOPE__"
```
`wwwroot/appsettings.Development.json`: the same keys with the real Task 5 values (tenant `46b33134-1b34-42b4-9848-6b40016d2e22`, `webClientId`, `apiScope`). None of these are secrets.

`.github/workflows/build.yml`: in `deploy-web` → "Substitute environment configuration", add three `-e` lines before the file path:
```yaml
            -e "s|__AZURE_AD_TENANT_ID__|${{ vars.AZURE_AD_TENANT_ID }}|" \
            -e "s|__AZURE_AD_WEB_CLIENT_ID__|${{ vars.AZURE_AD_WEB_CLIENT_ID }}|" \
            -e "s|__API_SCOPE__|${{ vars.API_SCOPE }}|" \
```

- [ ] **Step 9: Build and run all web tests**

Run: `dotnet build AzureSuite.slnx -warnaserror && dotnet test tests/AzureSuite.Web.Blazor.Tests`
Expected: build succeeds with 0 warnings; all tests PASS. Existing tests construct the API clients directly, so they don't touch MSAL.

- [ ] **Step 10: Commit**

```bash
git add frontends/AzureSuite.Web.Blazor tests/AzureSuite.Web.Blazor.Tests .github/workflows/build.yml
git commit -m "Add MSAL sign-in plumbing and API role claims to the web app"
```

---

### Task 7: Role-aware Blazor UI

**Files:**
- Modify: `frontends/AzureSuite.Web.Blazor/_Imports.razor`
- Modify: `frontends/AzureSuite.Web.Blazor/App.razor`
- Create: `frontends/AzureSuite.Web.Blazor/Pages/Authentication.razor`
- Create: `frontends/AzureSuite.Web.Blazor/Layout/RedirectToLogin.razor`
- Create: `frontends/AzureSuite.Web.Blazor/Layout/LoginDisplay.razor`
- Modify: `frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor`
- Modify: the six pages in `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/*.razor` and `Features/Ingestion/Pages/SubmitMessage.razor`
- Modify: `tests/AzureSuite.Web.Blazor.Tests/Layout/NavMenuTests.cs`, `App/RoutingTests.cs`, `Features/Catalog/Pages/ClientsListTests.cs`, plus any other page test that fails because it needs an authorization context
- Create: `tests/AzureSuite.Web.Blazor.Tests/Layout/LoginDisplayTests.cs`

**Interfaces:**
- Consumes: `AppPolicies` (Task 1); auth services registered in `Program.cs` (Task 6).
- Produces: nothing consumed later.

- [ ] **Step 1: Write the failing UI tests**

Replace `tests/AzureSuite.Web.Blazor.Tests/Layout/NavMenuTests.cs` with:
```csharp
using AzureSuite.Security;
using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class NavMenuTests : BunitContext
{
    private void SignInWith(params string[] policies)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetPolicies(policies);
    }

    [Fact]
    public void MarksTheCatalogLinkActiveAtTheAppRoot()
    {
        SignInWith(AppPolicies.CatalogRead, AppPolicies.SubmitMessages);

        var cut = Render<NavMenu>();

        cut.Find("a[href='/']").ClassList.Should().Contain("active");
        cut.Find("a[href='/ingestion']").ClassList.Should().NotContain("active");
    }

    [Fact]
    public void MarksTheIngestionLinkActiveOnTheIngestionRoute()
    {
        SignInWith(AppPolicies.CatalogRead, AppPolicies.SubmitMessages);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ingestion");

        var cut = Render<NavMenu>();

        cut.Find("a[href='/ingestion']").ClassList.Should().Contain("active");
        cut.Find("a[href='/']").ClassList.Should().NotContain("active");
    }

    [Fact]
    public void HidesCatalogLinksWithoutCatalogRead()
    {
        SignInWith(AppPolicies.SubmitMessages);

        var cut = Render<NavMenu>();

        cut.FindAll("a[href='/catalog/clients']").Should().BeEmpty();
        cut.FindAll("a[href='/catalog/routes']").Should().BeEmpty();
        cut.FindAll("a[href='/ingestion']").Should().ContainSingle();
    }

    [Fact]
    public void HidesTheIngestionLinkWithoutSubmitMessages()
    {
        SignInWith(AppPolicies.CatalogRead);

        var cut = Render<NavMenu>();

        cut.FindAll("a[href='/ingestion']").Should().BeEmpty();
    }
}
```

In `ClientsListTests`, add these tests and keep `RendersHeading`, giving it an authorized context first:
```csharp
        [Fact]
        public void ShowsTheRegisterButtonOnlyWithCatalogWrite()
        {
            var auth = AddAuthorization();
            auth.SetAuthorized("Oscar");
            auth.SetPolicies(AzureSuite.Security.AppPolicies.CatalogRead, AzureSuite.Security.AppPolicies.CatalogWrite);

            Render<ClientsList>().FindAll("a[href='/catalog/clients/register']").Should().ContainSingle();
        }

        [Fact]
        public void HidesTheRegisterButtonForAReader()
        {
            var auth = AddAuthorization();
            auth.SetAuthorized("Oscar");
            auth.SetPolicies(AzureSuite.Security.AppPolicies.CatalogRead);

            Render<ClientsList>().FindAll("a[href='/catalog/clients/register']").Should().BeEmpty();
        }
```
(`RendersHeading` gets `AddAuthorization().SetAuthorized("Oscar");` as its first line.)

`tests/AzureSuite.Web.Blazor.Tests/Layout/LoginDisplayTests.cs`:
```csharp
using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class LoginDisplayTests : BunitContext
{
    [Fact]
    public void SignedIn_ShowsTheNameAndASignOutButton()
    {
        AddAuthorization().SetAuthorized("Oscar Padron");

        var cut = Render<LoginDisplay>();

        cut.Find(".app-nav-user").TextContent.Should().Be("Oscar Padron");
        cut.Find("button").TextContent.Should().Be("sign out");
    }

    [Fact]
    public void SignedOut_ShowsASignInLink()
    {
        AddAuthorization();

        var cut = Render<LoginDisplay>();

        cut.Find("a[href='authentication/login']").TextContent.Should().Be("sign in");
    }
}
```

In `RoutingTests`, add to the constructor:
```csharp
        var auth = AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetPolicies(AzureSuite.Security.AppPolicies.CatalogRead, AzureSuite.Security.AppPolicies.CatalogWrite, AzureSuite.Security.AppPolicies.SubmitMessages);
```
Then add a test for the no-roles Review Focus case. It needs its own context, because the constructor already authorized with every policy:
```csharp
    [Fact]
    public void ASignedInUserWithoutTheRole_SeesAccessDenied()
    {
        using var context = new BunitContext();
        context.Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new ClientTelemetryLogger(context.JSInterop.JSRuntime));
        context.Services.AddSingleton(new ApiReferenceLinks([]));
        context.AddAuthorization().SetAuthorized("Nobody");

        var cut = context.Render<AppRoot>();

        cut.Markup.Should().Contain("You don't have access to this page");
        cut.Find(".app-nav").Should().NotBeNull();
    }
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests`
Expected: FAIL. `LoginDisplay` doesn't exist, the hide/show tests find links that should be hidden, and the access-denied text is missing.

- [ ] **Step 3: Implement the auth components**

`_Imports.razor`: add
```razor
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using AzureSuite.Security
```

`App.razor`:
```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="@routeData" DefaultLayout="typeof(MainLayout)">
            <NotAuthorized>
                @if (context.User.Identity?.IsAuthenticated != true)
                {
                    <RedirectToLogin />
                }
                else
                {
                    <ErrorState Message="You don't have access to this page. Ask an administrator for the role it needs." />
                }
            </NotAuthorized>
            <Authorizing>
                <p>Signing in...</p>
            </Authorizing>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(MainLayout)">
            <PageNotFound />
        </LayoutView>
    </NotFound>
</Router>
```

`Layout/RedirectToLogin.razor`:
```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized() => Navigation.NavigateToLogin("authentication/login");
}
```

`Pages/Authentication.razor`:
```razor
@page "/authentication/{action}"

<RemoteAuthenticatorView Action="@Action" />

@code {
    [Parameter]
    public string? Action { get; set; }
}
```

`Layout/LoginDisplay.razor`:
```razor
@inject NavigationManager Navigation

<AuthorizeView>
    <Authorized>
        <span class="app-nav-user">@context.User.Identity?.Name</span>
        <button type="button" class="app-nav-link" @onclick="SignOut">sign out</button>
    </Authorized>
    <NotAuthorized>
        <a href="authentication/login" class="app-nav-link">sign in</a>
    </NotAuthorized>
</AuthorizeView>

@code {
    private void SignOut() => Navigation.NavigateToLogout("authentication/logout");
}
```

`Layout/NavMenu.razor`:
```razor
<nav class="app-nav">
    <span class="app-nav-brand">
        <span class="app-mark" aria-hidden="true"></span>
        AzureSuite
    </span>
    <AuthorizeView Policy="@AppPolicies.CatalogRead">
        <NavLink href="/" Match="NavLinkMatch.All" class="app-nav-link">catalog</NavLink>
        <NavLink href="/catalog/clients" class="app-nav-link">clients</NavLink>
        <NavLink href="/catalog/routes" class="app-nav-link">routes</NavLink>
    </AuthorizeView>
    <AuthorizeView Policy="@AppPolicies.SubmitMessages">
        <NavLink href="/ingestion" class="app-nav-link">ingestion</NavLink>
    </AuthorizeView>
    <LoginDisplay />
</nav>
```
If `layout.css` has no rule for `.app-nav-user` or for a `button.app-nav-link`, add one: same font and colour as `.app-nav-link`, `margin-left: auto` on `.app-nav-user` so name and sign-out sit at the right, and `background: none; border: 0; cursor: pointer` on the button.

- [ ] **Step 4: Put policies on the pages and wrap the write buttons**

Add the attribute right after each page's `@page` line:

| Page | Attribute |
|---|---|
| `MessageTypesList.razor`, `ClientsList.razor`, `RoutesList.razor` | `@attribute [Authorize(Policy = AppPolicies.CatalogRead)]` |
| `RegisterMessageType.razor`, `RegisterClient.razor`, `RegisterRoute.razor` | `@attribute [Authorize(Policy = AppPolicies.CatalogWrite)]` |
| `SubmitMessage.razor` | `@attribute [Authorize(Policy = AppPolicies.SubmitMessages)]` |

In the three list pages, wrap the "Register…" `AppButton` inside `<Actions>`. For example, in `ClientsList.razor`:
```razor
    <Actions>
        <AuthorizeView Policy="@AppPolicies.CatalogWrite">
            <AppButton Href="/catalog/clients/register">Register client</AppButton>
        </AuthorizeView>
    </Actions>
```
(`MessageTypesList.razor`: wrap `<AppButton Href="/catalog/register" …>`; `RoutesList.razor`: wrap `<AppButton Href="/catalog/routes/register">`.)

- [ ] **Step 5: Fix the remaining page tests**

Run `dotnet test tests/AzureSuite.Web.Blazor.Tests`. Any page test that renders a component containing `AuthorizeView` now fails with a missing `Task<AuthenticationState>` cascading parameter. In each failing test class's constructor, add:
```csharp
        var auth = AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetPolicies(AzureSuite.Security.AppPolicies.CatalogRead, AzureSuite.Security.AppPolicies.CatalogWrite, AzureSuite.Security.AppPolicies.SubmitMessages);
```
Expected after the fixes: all tests PASS.

- [ ] **Step 6: Build warning-free**

Run: `dotnet build AzureSuite.slnx -warnaserror`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add frontends/AzureSuite.Web.Blazor tests/AzureSuite.Web.Blazor.Tests
git commit -m "Gate Blazor pages, nav and actions by role; add sign-in and sign-out"
```

---

### Task 8: Local end-to-end check and docs

**Files:**
- Modify: `docs/infrastructure.md`
- Modify: `docs/services/catalog.md`, `docs/services/ingestion.md`, `docs/services/web-blazor.md`
- Modify: `docs/superpowers/specs/2026-10-15-api-security-design.md` (record deviations)
- Modify: `docs/progress-log.md` (new dated entry at the top)

- [ ] **Step 1 (owner + agent): Local smoke test**

Start Catalog.Api, Ingestion.Api and the Blazor app with the VS Code launch configs. In the browser at `https://localhost:7206`:
1. You're redirected to Microsoft sign-in. After signing in, the nav shows your name, and catalog, clients, routes and ingestion all work.
2. Submitting a message returns a MessageId.

From a terminal:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST https://localhost:7185/messages -H "Content-Type: application/json" -d '{"messageType":"pacs.008","version":"1.0","payload":"{}"}'
```
Expected: `401`.

- [ ] **Step 2: Update `docs/infrastructure.md`**

Add a section "Identity (Entra ID) — deployed manually". It should cover: what `infra/identity/main.bicep` creates (the three registrations, roles, grants, assignments); the deploy command from Task 5; why CI doesn't run it; the limitations (no secrets, so use `az ad app credential reset`; no what-if; re-run on replication errors; Portal drift); and how to add a role (new fixed GUID in `roleIds`, an `appRoles` entry, a constant in `AppRoles`, a policy in `AddAzureSuitePolicies`).

- [ ] **Step 3: Update the service guides**

- `catalog.md` and `ingestion.md`: add an "Auth" column to the API tables (`CatalogRead`, `CatalogWrite`, `SubmitMessages`, anonymous for health). Add a "Calling it" note on how a producer gets a token:
  ```powershell
  $token = (Invoke-RestMethod -Method Post `
    -Uri "https://login.microsoftonline.com/46b33134-1b34-42b4-9848-6b40016d2e22/oauth2/v2.0/token" `
    -Body @{ client_id = "<producerClientId>"; client_secret = "<secret>"; grant_type = "client_credentials";
             scope = "api://46b33134-1b34-42b4-9848-6b40016d2e22/messaginghub-api/.default" }).access_token
  ```
  Remove the `ClientId` gap's mention of auth from `ingestion.md`'s "Known gaps" if it's listed.
- `web-blazor.md`: a "Sign-in and roles" section covering MSAL, `ApiRolesClaimsPrincipalFactory` and why it exists, `AuthorizeView` vs. API enforcement, and the three config placeholders.

- [ ] **Step 4: Record deviations in the spec**

In the spec, update: the identifier URI is `api://<tenantId>/messaginghub-api` (not `api://<appId>`); every policy also requires an authenticated user; the web app also gets an `openid profile offline_access` grant on Microsoft Graph; `deploy-infra.yml` uses a `!infra/identity/**` negation, because `paths` and `paths-ignore` can't be combined.

- [ ] **Step 5: Progress-log entry**

At the top of `docs/progress-log.md`, add a short dated section "API security — Entra ID app roles (2026-10-15)". It covers what was built, the manual identity deployment, and the rollout order: deploy identity → set vars → merge → verify on Azure.

- [ ] **Step 6: Commit**

```bash
git add docs
git commit -m "Document API security, identity deployment and roles"
```

---

## After merge (rollout on Azure, from the spec)

1. `develop` → `master` deploys both APIs and the web app.
2. Anonymous `curl -X POST https://app-messaginghub-ingestion-dev.azurewebsites.net/messages …` → `401`.
3. TestProducer token (snippet above) → `201`.
4. Signed in as the owner on the Static Web App → every page works.
5. Temporarily remove `Catalog.Admin` from the owner (Portal → Enterprise applications → MessagingHub-Api → Users and groups). After signing out and in, the Register buttons are gone and `POST /clients` with your token returns `403`. Then restore it by re-running the identity deployment.
