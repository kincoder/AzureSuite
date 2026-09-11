# Catalog.Web Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Catalog.Web, the first micro-frontend — a Blazor WASM app that lists and
registers message types against the real Catalog.Api — standalone-runnable first, then
exposed as a Custom Element and deployed to its own Azure Static Web App.

**Architecture:** Blazor WebAssembly SPA. A thin `CatalogApiClient` service wraps
`HttpClient` calls to Catalog.Api's two endpoints. Two pages (list, register) consume it.
Reuses `MessageTypeDto` from `Catalog.Application` for the read side (matches the existing
"don't duplicate DTOs" project rule); the register request is a small record defined
locally in Catalog.Web (Catalog.Api's own request record lives in an ASP.NET Core project
not meant to be referenced by a WASM client).

**Tech Stack:** .NET 10, Blazor WebAssembly, `Microsoft.AspNetCore.Components.CustomElements`,
xUnit + FluentAssertions for the one test project, Bicep, Azure Static Web Apps.

**Spec:** `docs/superpowers/specs/2026-09-11-catalog-web-design.md`

## Global Constraints

(Same as the Catalog service plan's Global Constraints — carried forward.)

- Hand-written code must be warning-free, nullable-enabled, no `#pragma warning disable`.
- Block-scoped namespaces everywhere, no file-scoped `namespace X;`.
- No top-level statements — explicit `public class Program` + `static void Main`.
- XML doc comments on every class and any non-obvious property.
- `tests/X.Tests` mirrors `frontends/X` 1:1 where tests exist.
- New/changed Azure resources go in `rg-messaginghub-dev`, naming
  `<type>-messaginghub-catalog-dev` pattern (Static Web App: `stapp-messaginghub-catalog-dev`).

---

## Task 1: Catalog.Api — InMemory seed data + CORS

**Files:**
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Create: `services/Catalog/Catalog.Api/Persistence/InMemorySeedData.cs`

**Interfaces:**
- Produces: `InMemorySeedData.Apply(CatalogDbContext context)` — inserts 2 sample
  `MessageType`s (`pacs.008`/`1.0`, `camt.054`/`1.0`) if the database is empty. Called once
  at startup, only when the InMemory provider is in use (never against real SQL).

- [ ] **Step 1: Write the seed data helper**

```csharp
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;

namespace AzureSuite.Catalog.Api.Persistence
{
    /// <summary>Sample message types inserted into the InMemory database on startup, so local
    /// frontend development never requires Azure SQL to be running.</summary>
    public static class InMemorySeedData
    {
        public static void Apply(CatalogDbContext context)
        {
            if (context.MessageTypes.Any())
            {
                return;
            }

            context.MessageTypes.AddRange(
                new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"),
                new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"));

            context.SaveChanges();
        }
    }
}
```

- [ ] **Step 2: Call it from `Program.cs`, only for the InMemory branch, and add CORS**

Modify the `if (string.IsNullOrEmpty(connectionString))` branch and add a CORS policy
allowing the local Catalog.Web dev server origin. Replace the existing connection-string
block and add CORS registration/use:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7100", "http://localhost:5100")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
var usingInMemory = string.IsNullOrEmpty(connectionString);
if (usingInMemory)
{
    builder.Services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("CatalogDb"));
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
}
```

After `var app = builder.Build();`, before `app.Run();`, add:

```csharp
app.UseCors();

if (usingInMemory)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    InMemorySeedData.Apply(context);
}
```

(`https://localhost:7100`/`http://localhost:5100` are placeholders — replace with
Catalog.Web's actual launch profile ports once Task 2 scaffolds it; if Task 2's ports
differ, come back and fix this list.)

- [ ] **Step 3: Verify manually**

Run: `dotnet run --project services/Catalog/Catalog.Api` (no `ConnectionStrings:CatalogDb`
user secret set, or run `dotnet user-secrets remove "ConnectionStrings:CatalogDb"` first if
testing this specific path — otherwise skip to Step 4 later and verify once Catalog.Web
exists to call it).
Expected: `curl -sk https://localhost:7184/message-types` returns the 2 seeded message types.

- [ ] **Step 4: Run the full test suite to confirm nothing broke**

Run: `dotnet test AzureSuite.slnx`
Expected: PASS, all existing tests green, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add services/Catalog/Catalog.Api
git commit -m "Add InMemory seed data and CORS to Catalog.Api"
```

---

## Task 2: Catalog.Web — scaffold, list + register pages

**Files:**
- Create: `frontends/Catalog.Web/Catalog.Web.csproj`
- Create: `frontends/Catalog.Web/Program.cs`
- Create: `frontends/Catalog.Web/wwwroot/index.html`
- Create: `frontends/Catalog.Web/wwwroot/appsettings.Development.json`
- Create: `frontends/Catalog.Web/Contracts/RegisterMessageTypeRequest.cs`
- Create: `frontends/Catalog.Web/Services/CatalogApiClient.cs`
- Create: `frontends/Catalog.Web/Pages/MessageTypesList.razor`
- Create: `frontends/Catalog.Web/Pages/MessageTypesList.razor.cs`
- Create: `frontends/Catalog.Web/Pages/RegisterMessageType.razor`
- Create: `frontends/Catalog.Web/Pages/RegisterMessageType.razor.cs`
- Create: `tests/Catalog.Web.Tests/Catalog.Web.Tests.csproj`
- Test: `tests/Catalog.Web.Tests/Services/CatalogApiClientTests.cs`

**Interfaces:**
- Consumes: `AzureSuite.Catalog.Application.MessageTypes.MessageTypeDto` (from
  `Catalog.Application`, referenced as a project reference).
- Produces: `RegisterMessageTypeRequest(string Name, string Version, string
  SchemaDefinition)`. `CatalogApiClient` with `Task<IReadOnlyList<MessageTypeDto>>
  GetMessageTypesAsync(CancellationToken)` and `Task<MessageTypeDto>
  RegisterMessageTypeAsync(RegisterMessageTypeRequest request, CancellationToken)`.

- [ ] **Step 1: Scaffold the Blazor WASM project**

Run:
```bash
dotnet new blazorwasm -n Catalog.Web -o frontends/Catalog.Web --empty
dotnet add frontends/Catalog.Web reference services/Catalog/Catalog.Application
dotnet sln AzureSuite.slnx add frontends/Catalog.Web
```

Note the launch profile's HTTPS/HTTP ports printed by the template (check
`frontends/Catalog.Web/Properties/launchSettings.json`) — if they differ from the
`https://localhost:7100`/`http://localhost:5100` placeholders used in Task 1 Step 2, update
that CORS policy now to match.

- [ ] **Step 2: Delete template placeholder content**

The `--empty` template still scaffolds `wwwroot/index.html` and `Program.cs` — keep those
(edited below), but remove any placeholder `Pages/`/`Layout/` content the template added
beyond what's listed in Files above, since this app has no navigation chrome yet (that's
the Shell's job later).

- [ ] **Step 3: Write `Program.cs`**

```csharp
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace AzureSuite.Catalog.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(builder.Configuration["CatalogApiBaseUrl"] ?? "https://localhost:7184")
            });
            builder.Services.AddScoped<CatalogApiClient>();

            await builder.Build().RunAsync();
        }
    }
}
```

- [ ] **Step 4: Write `wwwroot/appsettings.Development.json`**

```json
{
  "CatalogApiBaseUrl": "https://localhost:7184"
}
```

- [ ] **Step 5: Write `wwwroot/index.html`**

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Catalog</title>
    <base href="/" />
</head>
<body>
    <div id="app">Loading...</div>
    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

- [ ] **Step 6: Write `Contracts/RegisterMessageTypeRequest.cs`**

```csharp
namespace AzureSuite.Catalog.Web.Contracts
{
    /// <summary>Request body sent to Catalog.Api's POST /message-types endpoint.</summary>
    public record RegisterMessageTypeRequest(string Name, string Version, string SchemaDefinition);
}
```

- [ ] **Step 7: Write the failing `CatalogApiClient` test**

```bash
dotnet new xunit -n Catalog.Web.Tests -o tests/Catalog.Web.Tests
dotnet add tests/Catalog.Web.Tests reference frontends/Catalog.Web
dotnet add tests/Catalog.Web.Tests package FluentAssertions
dotnet sln AzureSuite.slnx add tests/Catalog.Web.Tests
rm tests/Catalog.Web.Tests/UnitTest1.cs
```

```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Contracts;
using AzureSuite.Catalog.Web.Services;
using FluentAssertions;
using Xunit;

namespace Catalog.Web.Tests.Services
{
    public class CatalogApiClientTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_respond(request));
            }
        }

        [Fact]
        public async Task GetMessageTypesAsync_ReturnsDeserializedList()
        {
            var expected = new List<MessageTypeDto>
            {
                new(Guid.NewGuid(), "pacs.008", "1.0", "{}", DateTime.UtcNow)
            };
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var result = await client.GetMessageTypesAsync(CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task RegisterMessageTypeAsync_ReturnsDeserializedDto()
        {
            var expected = new MessageTypeDto(Guid.NewGuid(), "camt.054", "1.0", "{}", DateTime.UtcNow);
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var result = await client.RegisterMessageTypeAsync(new RegisterMessageTypeRequest("camt.054", "1.0", "{}"), CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Web.Tests`
Expected: FAIL to compile — `CatalogApiClient` doesn't exist yet.

- [ ] **Step 9: Implement `CatalogApiClient`**

```csharp
using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Contracts;

namespace AzureSuite.Catalog.Web.Services
{
    /// <summary>Thin wrapper over Catalog.Api's HTTP endpoints.</summary>
    public class CatalogApiClient
    {
        private readonly HttpClient _httpClient;

        public CatalogApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<MessageTypeDto>> GetMessageTypesAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetFromJsonAsync<List<MessageTypeDto>>("/message-types", cancellationToken);
            return result ?? new List<MessageTypeDto>();
        }

        public async Task<MessageTypeDto> RegisterMessageTypeAsync(RegisterMessageTypeRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/message-types", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<MessageTypeDto>(cancellationToken))!;
        }
    }
}
```

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Web.Tests`
Expected: PASS, both tests green.

- [ ] **Step 11: Write `Pages/MessageTypesList.razor` + code-behind**

```razor
@page "/"

<h3>Registered Message Types</h3>

@if (MessageTypes is null)
{
    <p>Loading...</p>
}
else if (MessageTypes.Count == 0)
{
    <p>No message types registered yet.</p>
}
else
{
    <table>
        <thead>
            <tr><th>Name</th><th>Version</th><th>Registered</th></tr>
        </thead>
        <tbody>
            @foreach (var messageType in MessageTypes)
            {
                <tr>
                    <td>@messageType.Name</td>
                    <td>@messageType.Version</td>
                    <td>@messageType.RegisteredAtUtc</td>
                </tr>
            }
        </tbody>
    </table>
}

<a href="/register">Register a new message type</a>
```

```csharp
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    public partial class MessageTypesList : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        private IReadOnlyList<MessageTypeDto>? MessageTypes { get; set; }

        protected override async Task OnInitializedAsync()
        {
            MessageTypes = await ApiClient.GetMessageTypesAsync(CancellationToken.None);
        }
    }
}
```

- [ ] **Step 12: Write `Pages/RegisterMessageType.razor` + code-behind**

```razor
@page "/register"

<h3>Register Message Type</h3>

<EditForm Model="@Request" OnValidSubmit="@SubmitAsync">
    <div>
        <label>Name</label>
        <InputText @bind-Value="Request.Name" />
    </div>
    <div>
        <label>Version</label>
        <InputText @bind-Value="Request.Version" />
    </div>
    <div>
        <label>Schema Definition</label>
        <InputTextArea @bind-Value="Request.SchemaDefinition" />
    </div>
    <button type="submit">Register</button>
</EditForm>

@if (SubmittedMessage is not null)
{
    <p>@SubmittedMessage</p>
}
```

```csharp
using AzureSuite.Catalog.Web.Contracts;
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    public partial class RegisterMessageType : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        private RegisterMessageTypeRequest Request { get; set; } = new(string.Empty, string.Empty, string.Empty);

        private string? SubmittedMessage { get; set; }

        private async Task SubmitAsync()
        {
            var dto = await ApiClient.RegisterMessageTypeAsync(Request, CancellationToken.None);
            SubmittedMessage = $"Registered {dto.Name} v{dto.Version}.";
            Request = new RegisterMessageTypeRequest(string.Empty, string.Empty, string.Empty);
        }
    }
}
```

Note: `RegisterMessageTypeRequest` is a `record` with positional constructor parameters,
which default to init-only properties — the same gotcha documented in the project's
progress log. `@bind-Value` on `InputText` needs a settable property, so `Request` is
*reassigned* on each field via the record's `with`-less plain constructor here — actually,
since `InputText`'s two-way binding calls the property setter directly, and a positional
record's properties ARE settable (`init` only blocks post-construction mutation from
*outside* the type, but records generate `{ get; init; }` — `init` accessors cannot be
called after construction, including via data binding). **This will fail to compile or
bind.** Fix: make `RegisterMessageTypeRequest` a plain class with `{ get; set; }`
properties instead of a positional record, matching the documented gotcha
(`CreatePacs008MessageRequest` in the superseded build hit the same issue). Update Step 6
accordingly before implementing this step:

```csharp
namespace AzureSuite.Catalog.Web.Contracts
{
    /// <summary>Request body sent to Catalog.Api's POST /message-types endpoint.</summary>
    public class RegisterMessageTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SchemaDefinition { get; set; } = string.Empty;
    }
}
```

(This changes `Contracts/RegisterMessageTypeRequest.cs` from Step 6 — apply this version
instead of the record shown there. The `CatalogApiClientTests` calls in Step 7 that use
`new RegisterMessageTypeRequest("camt.054", "1.0", "{}")` positional syntax need updating
to object-initializer syntax: `new RegisterMessageTypeRequest { Name = "camt.054", Version = "1.0", SchemaDefinition = "{}" }`.)

- [ ] **Step 13: Run the full test suite**

Run: `dotnet test AzureSuite.slnx`
Expected: PASS, every test project green, 0 warnings.

- [ ] **Step 14: Run Catalog.Api and Catalog.Web together, verify manually**

Run (two terminals):
```bash
dotnet run --project services/Catalog/Catalog.Api
dotnet run --project frontends/Catalog.Web
```
Open the Catalog.Web URL in a browser. Expected: the list page shows the 2 seeded message
types (from Task 1); the register form successfully adds a third and it appears on reload.

- [ ] **Step 15: Commit**

```bash
git add frontends/Catalog.Web tests/Catalog.Web.Tests AzureSuite.slnx
git commit -m "Add Catalog.Web: list and register pages against Catalog.Api"
```

---

## Task 3: Expose Catalog.Web as a Custom Element

**Files:**
- Modify: `frontends/Catalog.Web/Catalog.Web.csproj`
- Modify: `frontends/Catalog.Web/Program.cs`
- Modify: `frontends/Catalog.Web/wwwroot/index.html`

**Interfaces:**
- Produces: a `<catalog-app>` custom element wrapping `MessageTypesList`, loadable by a
  future Shell without Catalog.Web needing to know the Shell exists.

- [ ] **Step 1: Add the CustomElements package**

Run:
```bash
dotnet add frontends/Catalog.Web package Microsoft.AspNetCore.Components.CustomElements
```

- [ ] **Step 2: Register the custom element in `Program.cs`**

Add before `await builder.Build().RunAsync();`:

```csharp
builder.RootComponents.RegisterCustomElement<AzureSuite.Catalog.Web.Pages.MessageTypesList>("catalog-app");
```

- [ ] **Step 3: Update `wwwroot/index.html` to use the custom element**

Replace `<div id="app">Loading...</div>` with:

```html
<catalog-app>Loading...</catalog-app>
```

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test AzureSuite.slnx`
Expected: PASS, 0 warnings.

- [ ] **Step 5: Verify manually**

Run: `dotnet run --project frontends/Catalog.Web`
Expected: the app still renders standalone (via the custom element now, not a plain root
component) — confirms this doesn't break standalone running while also proving it's
composable.

- [ ] **Step 6: Commit**

```bash
git add frontends/Catalog.Web
git commit -m "Expose Catalog.Web root component as a Custom Element"
```

---

## Task 4: Infra — Static Web App for Catalog.Web

**Files:**
- Modify: `infra/main.bicep`
- Create: `infra/modules/catalog/staticwebapp.bicep`

**Interfaces:**
- Produces: an Azure Static Web App resource `stapp-messaginghub-catalog-dev` in
  `rg-messaginghub-dev`, output `staticWebAppDefaultHostname`.

- [ ] **Step 1: Write `infra/modules/catalog/staticwebapp.bicep`**

```bicep
param location string
param staticWebAppName string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
```

- [ ] **Step 2: Add the module to `infra/main.bicep`**

Add alongside the existing `catalogKeyVault`/`catalogSql` modules:

```bicep
module catalogStaticWebApp 'modules/catalog/staticwebapp.bicep' = {
  name: 'catalogStaticWebApp'
  params: {
    location: location
    staticWebAppName: 'stapp-messaginghub-catalog-dev'
  }
}
```

And add an output:

```bicep
output catalogStaticWebAppHostname string = catalogStaticWebApp.outputs.defaultHostname
```

- [ ] **Step 3: Validate with what-if**

Run:
```bash
az deployment group what-if --resource-group rg-messaginghub-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```
Expected: shows one new resource (the Static Web App), existing resources unchanged.

- [ ] **Step 4: Deploy**

Run:
```bash
az deployment group create --resource-group rg-messaginghub-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```
Expected: deployment succeeds; note the `catalogStaticWebAppHostname` output.

- [ ] **Step 5: Build and deploy Catalog.Web's static output**

Run:
```bash
dotnet publish frontends/Catalog.Web -c Release -o frontends/Catalog.Web/publish
```
Get a deployment token and deploy via the Static Web Apps CLI (`swa deploy`) or
`az staticwebapp` — exact command depends on what's installed; if `swa`/`staticwebapp` CLI
tooling isn't present yet, install it first (`npm install -g @azure/static-web-apps-cli`)
and record the install step in the progress log, same as prior tooling installs.

- [ ] **Step 6: Verify manually**

Open `https://<catalogStaticWebAppHostname>` in a browser. Expected: the deployed
Catalog.Web loads and — once its `CatalogApiBaseUrl` config is confirmed pointed at the
real deployed Catalog.Api — the list/register pages work against the live Azure SQL data
registered earlier in the Catalog service work.

- [ ] **Step 7: Update the progress log**

Add a section documenting: the Static Web App deployment, the CLI tooling used, and
confirmation Catalog.Web works end-to-end against the deployed Catalog.Api.

- [ ] **Step 8: Commit**

```bash
git add infra docs/progress-log.md
git commit -m "Add Catalog.Web Static Web App infra and deploy"
```

---

## Self-Review Notes

- **Spec coverage:** all 5 build-order steps from the design spec map to Tasks 1-4 above
  (Task 2 covers spec steps 2; Task 3 covers step 3; Task 4 covers step 4). The Shell
  (spec step 5) is explicitly out of scope, as the spec states.
- **Placeholder scan:** Task 4 Step 5's exact SWA deploy command is left flexible
  (depends on what CLI tooling is already installed) rather than a fabricated exact
  command — this is a legitimate environment-dependent step, not a lazy placeholder; the
  step still says exactly what to verify and record.
- **Type consistency:** fixed the `RegisterMessageTypeRequest` record/class gotcha inline
  in Task 2 Step 12 before it could become a real bug, consistent with the same issue
  already documented from the superseded build.
