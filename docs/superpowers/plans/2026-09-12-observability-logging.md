# Shared Observability (Serilog + Application Insights) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire up shared logging/observability across the solution: a `AzureSuite.Observability` library that every backend service calls once to get Serilog wired to whatever sinks its own `appsettings.json` declares (Application Insights always when a connection string is configured, Windows Event Log only where the config says so — no `IsDevelopment()` branching in code), plus the equivalent Application Insights JavaScript SDK wiring for Blazor WASM MFEs, backed by one shared Application Insights resource in Azure.

**Architecture:** `services/Shared/AzureSuite.Observability` (backend) exposes `builder.AddAzureSuiteLogging(serviceName)`, which reads the `Serilog` config section via `Serilog.Settings.Configuration` (sink selection lives entirely in `appsettings*.json`) and additionally wires the Application Insights sink with a `CloudRoleNameTelemetryInitializer` (the one piece of "which app is this" tagging that can't be expressed as JSON) whenever `ApplicationInsights:ConnectionString` is configured. `frontends/AzureSuite.Web.UI/wwwroot/applicationInsights.js` mirrors this for the browser using Microsoft's official Application Insights JS SDK loader source, exposing `initAppInsights(connectionString, roleName)`, called once from each MFE's `Program.cs` (parallel to the backend's one-line call). A new shared Bicep module provisions one Application Insights resource (backed by a Log Analytics workspace) that every service/MFE points its connection string at.

**Tech Stack:** Serilog.AspNetCore, Serilog.Settings.Configuration, Serilog.Sinks.ApplicationInsights, Serilog.Sinks.EventLog, Microsoft.ApplicationInsights (backend); Microsoft's official Application Insights JavaScript SDK loader source (frontend); Bicep (`Microsoft.OperationalInsights/workspaces` + `Microsoft.Insights/components`).

**Spec:** `docs/superpowers/specs/2026-09-11-observability-logging-design.md`

## Global Constraints

- Target framework `net10.0`, `Nullable`/`ImplicitUsings` enabled on every new project — matches every existing project in this repo.
- Package versions (verified against NuGet as current stable at plan time): `Serilog.AspNetCore` 10.0.0, `Serilog.Settings.Configuration` 10.0.1, `Serilog.Sinks.ApplicationInsights` 5.0.1, `Serilog.Sinks.EventLog` 4.0.0, `Microsoft.ApplicationInsights` 3.1.2.
- Sink selection (Event Log present/absent, minimum level) is driven entirely by `appsettings*.json` — no `IsDevelopment()`/`OperatingSystem.IsWindows()` branches in `AzureSuite.Observability`. Only the Application Insights cloud-role tagging stays in code.
- One shared Application Insights resource for the whole solution; every service/MFE tags its own telemetry with its own name via the "cloud role name" mechanism.
- `appsettings.Development.json` files carry real local values and are not committed with secrets — `services/Catalog/Catalog.Api/appsettings.Development.json` is already gitignored; `frontends/Catalog.Web/wwwroot/appsettings.Development.json` is already tracked in git (a pre-existing repo state) and stays a placeholder (empty connection string) in this plan — the plan does not ask you to commit a real value there.
- New test project(s) follow this repo's 1:1 `X.Y.Tests` convention, warning-free build.
- Every new `.csproj` is added to `AzureSuite.slnx` under the matching folder.

---

### Task 1: `AzureSuite.Observability` library — `CloudRoleNameTelemetryInitializer`

**Files:**
- Create: `services/Shared/AzureSuite.Observability/AzureSuite.Observability.csproj`
- Create: `services/Shared/AzureSuite.Observability/CloudRoleNameTelemetryInitializer.cs`
- Test: `tests/AzureSuite.Observability.Tests/AzureSuite.Observability.Tests.csproj`
- Test: `tests/AzureSuite.Observability.Tests/CloudRoleNameTelemetryInitializerTests.cs`
- Modify: `AzureSuite.slnx` (add both projects, under a new `/services/Shared/` folder and the existing `/tests/` folder)

**Interfaces:**
- Produces: `CloudRoleNameTelemetryInitializer` (implements `Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer`), constructor `CloudRoleNameTelemetryInitializer(string roleName)`, method `void Initialize(ITelemetry telemetry)` that sets `telemetry.Context.Cloud.RoleName = roleName`. Consumed by Task 2's `AddAzureSuiteLogging`.

- [ ] **Step 1: Create the library project**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AzureSuite.Observability</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="10.0.1" />
    <PackageReference Include="Serilog.Sinks.ApplicationInsights" Version="5.0.1" />
    <PackageReference Include="Serilog.Sinks.EventLog" Version="4.0.0" />
    <PackageReference Include="Microsoft.ApplicationInsights" Version="3.1.2" />
  </ItemGroup>

</Project>
```

Save as `services/Shared/AzureSuite.Observability/AzureSuite.Observability.csproj`.

- [ ] **Step 2: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\services\Shared\AzureSuite.Observability\AzureSuite.Observability.csproj" />
  </ItemGroup>

</Project>
```

Save as `tests/AzureSuite.Observability.Tests/AzureSuite.Observability.Tests.csproj`.

- [ ] **Step 3: Register both projects in the solution**

Add a new `/services/Shared/` folder and the test project to `AzureSuite.slnx`:

```xml
  <Folder Name="/services/Shared/">
    <Project Path="services/Shared/AzureSuite.Observability/AzureSuite.Observability.csproj" />
  </Folder>
```

(Add this folder block alongside the existing `/services/Catalog/` folder.) In the existing `/tests/` folder, add:

```xml
    <Project Path="tests/AzureSuite.Observability.Tests/AzureSuite.Observability.Tests.csproj" />
```

- [ ] **Step 4: Write the failing test**

```csharp
using AzureSuite.Observability;
using FluentAssertions;
using Microsoft.ApplicationInsights.DataContracts;

namespace AzureSuite.Observability.Tests;

public class CloudRoleNameTelemetryInitializerTests
{
    [Fact]
    public void InitializeSetsCloudRoleNameOnTelemetryContext()
    {
        var initializer = new CloudRoleNameTelemetryInitializer("Catalog.Api");
        var telemetry = new TraceTelemetry("test message");

        initializer.Initialize(telemetry);

        telemetry.Context.Cloud.RoleName.Should().Be("Catalog.Api");
    }
}
```

Save as `tests/AzureSuite.Observability.Tests/CloudRoleNameTelemetryInitializerTests.cs`.

- [ ] **Step 5: Run it to verify it fails**

Run: `dotnet test tests/AzureSuite.Observability.Tests`
Expected: FAIL — compile error, `CloudRoleNameTelemetryInitializer` does not exist. (`TraceTelemetry` needs the `Microsoft.ApplicationInsights` package, already referenced transitively via the project reference — if the compiler can't resolve it, add `Microsoft.ApplicationInsights` version `3.1.2` directly to the test csproj's `PackageReference`s.)

- [ ] **Step 6: Implement `CloudRoleNameTelemetryInitializer`**

```csharp
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace AzureSuite.Observability;

/// <summary>
/// Tags every telemetry item with a fixed "cloud role name" so multiple services sharing
/// one Application Insights resource stay distinguishable in the Application Map, Live
/// Metrics, and log queries (cloud_RoleName == roleName).
/// </summary>
public sealed class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _roleName;

    public CloudRoleNameTelemetryInitializer(string roleName)
    {
        _roleName = roleName;
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = _roleName;
    }
}
```

Save as `services/Shared/AzureSuite.Observability/CloudRoleNameTelemetryInitializer.cs`.

- [ ] **Step 7: Run it to verify it passes**

Run: `dotnet test tests/AzureSuite.Observability.Tests`
Expected: PASS (1 test), 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add services/Shared/AzureSuite.Observability tests/AzureSuite.Observability.Tests AzureSuite.slnx
git commit -m "$(cat <<'EOF'
Add AzureSuite.Observability library with CloudRoleNameTelemetryInitializer

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `AddAzureSuiteLogging` extension method

**Files:**
- Create: `services/Shared/AzureSuite.Observability/LoggingBuilderExtensions.cs`

**Interfaces:**
- Consumes: `CloudRoleNameTelemetryInitializer` from Task 1.
- Produces: `public static class LoggingBuilderExtensions` with
  `public static WebApplicationBuilder AddAzureSuiteLogging(this WebApplicationBuilder builder, string serviceName)`.
  Consumed by Task 3 (`Catalog.Api`).

This method is composition of third-party library calls with no independent branching
logic worth unit testing (per the spec's Testing section) — its correctness is verified
manually in Task 6.

- [ ] **Step 1: Implement `LoggingBuilderExtensions`**

```csharp
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace AzureSuite.Observability;

/// <summary>Wires Serilog for a service, with sink selection driven entirely by that
/// service's own appsettings configuration (see the "Serilog" config section) rather
/// than environment checks in code.</summary>
public static class LoggingBuilderExtensions
{
    public static WebApplicationBuilder AddAzureSuiteLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName);

        var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            telemetryConfiguration.ConnectionString = connectionString;
            telemetryConfiguration.TelemetryInitializers.Add(new CloudRoleNameTelemetryInitializer(serviceName));

            loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
                telemetryConfiguration,
                TelemetryConverter.Traces);
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        builder.Host.UseSerilog();

        return builder;
    }
}
```

Save as `services/Shared/AzureSuite.Observability/LoggingBuilderExtensions.cs`.

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build services/Shared/AzureSuite.Observability/AzureSuite.Observability.csproj`
Expected: `Build succeeded`, 0 warnings, 0 errors. If `TelemetryConverter` or `WriteTo.ApplicationInsights` don't resolve, check the exact namespace `Serilog.Sinks.ApplicationInsights.TelemetryConverters` (that's where `TelemetryConverter.Traces` lives in this package version) and adjust the `using` accordingly — the extension method `WriteTo.ApplicationInsights(TelemetryConfiguration, ITelemetryConverter)` itself lives in the root `Serilog` namespace via the sink's assembly, no extra `using` needed for that part.

- [ ] **Step 3: Commit**

```bash
git add services/Shared/AzureSuite.Observability/LoggingBuilderExtensions.cs
git commit -m "$(cat <<'EOF'
Add AddAzureSuiteLogging extension wiring Serilog from config + App Insights

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Wire `Catalog.Api` to `AzureSuite.Observability`

**Files:**
- Modify: `services/Catalog/Catalog.Api/Catalog.Api.csproj`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Modify: `services/Catalog/Catalog.Api/appsettings.json`
- Modify: `services/Catalog/Catalog.Api/appsettings.Development.json` (gitignored — real local edit only, not committed)

**Interfaces:**
- Consumes: `AddAzureSuiteLogging(this WebApplicationBuilder, string)` from Task 2.

- [ ] **Step 1: Add the project reference**

In `services/Catalog/Catalog.Api/Catalog.Api.csproj`, add to the `ItemGroup` containing `PackageReference`s a new `ItemGroup` (or extend an existing one) with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Shared\AzureSuite.Observability\AzureSuite.Observability.csproj" />
  </ItemGroup>
```

(Path is relative to `services/Catalog/Catalog.Api/` — `..\..\Shared\...` reaches `services/Shared/...`.)

- [ ] **Step 2: Call it from `Program.cs`**

In `services/Catalog/Catalog.Api/Program.cs`, add `using AzureSuite.Observability;` to the usings, and immediately after `var builder = WebApplication.CreateBuilder(args);` add:

```csharp
builder.AddAzureSuiteLogging("Catalog.Api");
```

- [ ] **Step 3: Add the `Serilog`/`ApplicationInsights` sections to `appsettings.json`**

`services/Catalog/Catalog.Api/appsettings.json` becomes:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApplicationInsights": {
    "ConnectionString": ""
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": []
  }
}
```

- [ ] **Step 4: Add the local Event Log sink to `appsettings.Development.json`**

`services/Catalog/Catalog.Api/appsettings.Development.json` (not committed, real connection string
filled in once Task 4's Bicep deployment prints it — leave it empty for now, still works, just no
Application Insights sink locally until filled in) becomes:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ApplicationInsights": {
    "ConnectionString": ""
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "EventLog", "Args": { "source": "Catalog.Api", "manageEventSource": true } }
    ]
  }
}
```

- [ ] **Step 5: Build and confirm no regressions**

Run: `dotnet build services/Catalog/Catalog.Api/Catalog.Api.csproj`
Expected: `Build succeeded`, 0 warnings, 0 errors.

Run: `dotnet test AzureSuite.slnx`
Expected: all existing tests still pass (this task changes no testable behavior, only startup wiring).

- [ ] **Step 6: Commit**

```bash
git add services/Catalog/Catalog.Api/Catalog.Api.csproj services/Catalog/Catalog.Api/Program.cs services/Catalog/Catalog.Api/appsettings.json
git commit -m "$(cat <<'EOF'
Wire Catalog.Api to AzureSuite.Observability logging

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

(`appsettings.Development.json` is gitignored and won't be picked up by `git add` on that path — nothing further to do there.)

---

### Task 4: Shared Application Insights Bicep module

**Files:**
- Create: `infra/modules/shared/appinsights.bicep`
- Modify: `infra/main.bicep`

**Interfaces:**
- Produces: Bicep output `connectionString` (the Application Insights connection string), surfaced as a new `main.bicep` output `sharedAppInsightsConnectionString`. Consumed manually in Task 3/5's `appsettings.Development.json` files (paste the value in after deploying).

- [ ] **Step 1: Create the module**

```bicep
param location string
param logAnalyticsWorkspaceName string
param appInsightsName string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
  }
}

output connectionString string = appInsights.properties.ConnectionString
```

Save as `infra/modules/shared/appinsights.bicep`.

- [ ] **Step 2: Wire it into `main.bicep`**

Add a new module declaration to `infra/main.bicep` (alongside the existing `catalogKeyVault`/`catalogSql`/`catalogStaticWebApp` modules) and a new output:

```bicep
module sharedAppInsights 'modules/shared/appinsights.bicep' = {
  name: 'sharedAppInsights'
  params: {
    location: location
    logAnalyticsWorkspaceName: 'law-messaginghub-dev'
    appInsightsName: 'appi-messaginghub-dev'
  }
}
```

And add to the existing `output` lines at the bottom of `main.bicep`:

```bicep
output sharedAppInsightsConnectionString string = sharedAppInsights.outputs.connectionString
```

- [ ] **Step 3: Validate the Bicep compiles**

Run: `az bicep build --file infra/main.bicep --stdout > /dev/null`
Expected: no errors printed (the command succeeds silently on success; any Bicep syntax/type
error prints to stderr and the command exits non-zero).

- [ ] **Step 4: Deploy**

Run (from the repo root, same resource group used by prior deployments per `docs/progress-log.md`):

```bash
az deployment group create \
  --resource-group rg-messaginghub-dev \
  --template-file infra/main.bicep \
  --parameters sqlAdminLogin=<existing value> sqlAdminPassword=<existing value> principalId=<existing value>
```

Use `az deployment group what-if` first with the same arguments to confirm it shows only the
new `sharedAppInsights` module's resources (a Log Analytics workspace and an Application
Insights component) being added, with no changes to existing Catalog resources — matching the
verification approach already used for the Catalog Static Web App deployment.

- [ ] **Step 5: Capture the connection string**

Run: `az deployment group show --resource-group rg-messaginghub-dev --name main --query properties.outputs.sharedAppInsightsConnectionString.value -o tsv`

Paste this value into `services/Catalog/Catalog.Api/appsettings.Development.json`'s
`ApplicationInsights:ConnectionString` (from Task 3) and into
`frontends/Catalog.Web/wwwroot/appsettings.Development.json`'s new
`ApplicationInsightsConnectionString` key (added in Task 5) — both are local-only edits used to
verify Task 6, not committed as part of this task's commit.

- [ ] **Step 6: Commit the infra changes**

```bash
git add infra/modules/shared/appinsights.bicep infra/main.bicep
git commit -m "$(cat <<'EOF'
Add shared Application Insights + Log Analytics Bicep module

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Frontend Application Insights wiring for Catalog.Web

**Files:**
- Create: `frontends/AzureSuite.Web.UI/wwwroot/applicationInsights.js`
- Create: `frontends/Catalog.Web/wwwroot/appsettings.json`
- Modify: `frontends/Catalog.Web/wwwroot/appsettings.Development.json`
- Modify: `frontends/Catalog.Web/wwwroot/index.html`
- Modify: `frontends/Catalog.Web/Program.cs`

**Interfaces:**
- Produces: global JS function `initAppInsights(connectionString, roleName)`, called via
  `IJSRuntime.InvokeVoidAsync` from `Catalog.Web`'s `Program.cs`.

- [ ] **Step 1: Add the shared JS file**

Create `frontends/AzureSuite.Web.UI/wwwroot/applicationInsights.js` with this exact content
(the unminified body is Microsoft's official Application Insights JavaScript SDK loader
source — https://github.com/microsoft/ApplicationInsights-JS/blob/main/AISKU/snippet/snippet.js
— refactored from a self-invoking script into a named function so it can be parameterized
instead of pasted inline per page):

```js
// Refactored from Microsoft's official Application Insights JavaScript SDK loader
// (https://github.com/microsoft/ApplicationInsights-JS/blob/main/AISKU/snippet/snippet.js)
// into a callable function so each MFE can initialize it with its own connection string
// and "cloud role name" instead of pasting the inline snippet into every index.html.
function loadApplicationInsightsSnippet(win, doc, snipConfig) {
    var locn = win.location;
    var helpLink = "https://go.microsoft.com/fwlink/?linkid=2128109";
    var scriptText = "script";
    var strInstrumentationKey = "instrumentationKey";
    var strIngestionendpoint = "ingestionendpoint";
    var strDisableExceptionTracking = "disableExceptionTracking";
    var strAiDevice = "ai.device.";
    var strAiOperationName = "ai.operation.name";
    var strAiSdkVersion = "ai.internal.sdkVersion";
    var strToLowerCase = "toLowerCase";
    var strConStringIKey = strInstrumentationKey[strToLowerCase]();
    var strEmpty = "";
    var strUndefined = "undefined";
    var strCrossOrigin = "crossOrigin";

    var strPostMethod = "POST";
    var sdkInstanceName = "appInsightsSDK";
    var aiName = snipConfig.name || "appInsights";
    if (snipConfig.name || win[sdkInstanceName]) {
        win[sdkInstanceName] = aiName;
    }
    var aiSdk = win[aiName] || (function (aiConfig) {
        var loadFailed = false;
        var handled = false;
        var appInsights = {
            initialize: true,
            queue: [],
            sv: "10",
            version: 2.0,
            config: aiConfig
        };
        function _parseConnectionString() {
            var fields = {};
            var connectionString = aiConfig.connectionString;
            if (connectionString) {
                var kvPairs = connectionString.split(";");
                for (var lp = 0; lp < kvPairs.length; lp++) {
                    var kvParts = kvPairs[lp].split("=");

                    if (kvParts.length === 2) {
                        fields[kvParts[0][strToLowerCase]()] = kvParts[1];
                    }
                }
            }

            if (!fields[strIngestionendpoint]) {
                var endpointSuffix = fields.endpointsuffix;
                var fLocation = endpointSuffix ? fields.location : null;
                fields[strIngestionendpoint] = "https://" + (fLocation ? fLocation + "." : strEmpty) + "dc." + (endpointSuffix || "services.visualstudio.com");
            }

            return fields;
        }

        function _sendEvents(evts, endpointUrl) {
            if (JSON) {
                var sender = win.fetch;
                if (sender && !snipConfig.useXhr) {
                    sender(endpointUrl, { method: strPostMethod, body: JSON.stringify(evts), mode: "cors" });
                } else if (XMLHttpRequest) {
                    var xhr = new XMLHttpRequest();
                    xhr.open(strPostMethod, endpointUrl);
                    xhr.setRequestHeader("Content-type", "application/json");
                    xhr.send(JSON.stringify(evts));
                }
            }
        }

        function _reportFailure(targetSrc) {
            var conString = _parseConnectionString();
            var iKey = conString[strConStringIKey] || aiConfig[strInstrumentationKey] || strEmpty;
            var ingest = conString[strIngestionendpoint];
            var endpointUrl = ingest ? ingest + "/v2/track" : aiConfig.endpointUrl;

            var message = "SDK LOAD Failure: Failed to load Application Insights SDK script (See stack for details)";
            var evts = [];
            evts.push(_createException(iKey, message, targetSrc, endpointUrl));
            evts.push(_createInternal(iKey, message, targetSrc, endpointUrl));

            _sendEvents(evts, endpointUrl);
        }

        function _getTime() {
            var date = new Date();
            function pad(num) {
                var r = strEmpty + num;
                if (r.length === 1) {
                    r = "0" + r;
                }

                return r;
            }

            return date.getUTCFullYear()
                + "-" + pad(date.getUTCMonth() + 1)
                + "-" + pad(date.getUTCDate())
                + "T" + pad(date.getUTCHours())
                + ":" + pad(date.getUTCMinutes())
                + ":" + pad(date.getUTCSeconds())
                + "." + String((date.getUTCMilliseconds() / 1000).toFixed(3)).slice(2, 5)
                + "Z";
        }

        function _createEnvelope(iKey, theType) {
            var tags = {};
            var type = "Browser";
            tags[strAiDevice + "id"] = type[strToLowerCase]();
            tags[strAiDevice + "type"] = type;
            tags[strAiOperationName] = locn && locn.pathname || "_unknown_";
            tags[strAiSdkVersion] = "javascript:snippet_" + (appInsights.sv || appInsights.version);

            return {
                time: _getTime(),
                iKey: iKey,
                name: "Microsoft.ApplicationInsights." + iKey.replace(/-/g, strEmpty) + "." + theType,
                sampleRate: 100,
                tags: tags,
                data: {
                    baseData: {
                        ver: 2
                    }
                }
            };
        }

        function _createInternal(iKey, message, targetSrc, endpointUrl) {
            var envelope = _createEnvelope(iKey, "Message");
            var data = envelope.data;
            data.baseType = "MessageData";
            var baseData = data.baseData;
            baseData.message = "AI (Internal): 99 message:\"" + (message + " (" + targetSrc + ")").replace(/\"/g, strEmpty) + "\"";
            baseData.properties = {
                endpoint: endpointUrl
            };

            return envelope;
        }

        function _createException(iKey, message, targetSrc, endpointUrl) {
            var envelope = _createEnvelope(iKey, "Exception");
            var data = envelope.data;
            data.baseType = "ExceptionData";
            data.baseData.exceptions = [{
                typeName: "SDKLoadFailed",
                message: message.replace(/\./g, "-"),
                hasFullStack: false,
                stack: message + "\nSnippet failed to load [" + targetSrc + "] -- Telemetry is disabled\nHelp Link: " + helpLink + "\nHost: " + (locn && locn.pathname || "_unknown_") + "\nEndpoint: " + endpointUrl,
                parsedStack: []
            }];

            return envelope;
        }

        var targetSrc = aiConfig.url || snipConfig.src;
        if (targetSrc) {
            function _handleError(evt) {
                loadFailed = true;
                appInsights.queue = [];
                if (!handled) {
                    handled = true;
                    _reportFailure(targetSrc);
                }
            }

            function _handleLoad(evt, isAbort) {
                if (!handled) {
                    setTimeout(function () {
                        if (isAbort || !appInsights.core) {
                            _handleError();
                        }
                    }, 500);
                }
            }

            function _createScript() {
                var scriptElement = doc.createElement(scriptText);
                scriptElement.src = targetSrc;

                var crossOrigin = snipConfig[strCrossOrigin];
                if ((crossOrigin || crossOrigin === "") && scriptElement[strCrossOrigin] != strUndefined) {
                    scriptElement[strCrossOrigin] = crossOrigin;
                }

                scriptElement.onload = _handleLoad;
                scriptElement.onerror = _handleError;

                scriptElement.onreadystatechange = function (evt, isAbort) {
                    if (scriptElement.readyState === "loaded" || scriptElement.readyState === "complete") {
                        _handleLoad(evt, isAbort);
                    }
                };

                return scriptElement;
            }

            var theScript = _createScript();
            if (snipConfig.ld < 0) {
                var headNode = doc.getElementsByTagName("head")[0];
                headNode.appendChild(theScript);
            } else {
                setTimeout(function () {
                    doc.getElementsByTagName(scriptText)[0].parentNode.appendChild(theScript);
                }, snipConfig.ld || 0);
            }
        }

        try {
            appInsights.cookie = doc.cookie;
        } catch (e) { }

        function _createMethods(methods) {
            while (methods.length) {
                (function (name) {
                    appInsights[name] = function () {
                        var originalArguments = arguments;
                        if (!loadFailed) {
                            appInsights.queue.push(function () {
                                appInsights[name].apply(appInsights, originalArguments);
                            });
                        }
                    };
                })(methods.pop());
            }
        }

        var track = "track";
        var trackPage = "TrackPage";
        var trackEvent = "TrackEvent";
        _createMethods([track + "Event",
            track + "PageView",
            track + "Exception",
            track + "Trace",
            track + "DependencyData",
            track + "Metric",
            track + "PageViewPerformance",
            "start" + trackPage,
            "stop" + trackPage,
            "start" + trackEvent,
            "stop" + trackEvent,
            "addTelemetryInitializer",
            "setAuthenticatedUserContext",
            "clearAuthenticatedUserContext",
            "flush"]);

        appInsights['SeverityLevel'] = {
            Verbose: 0,
            Information: 1,
            Warning: 2,
            Error: 3,
            Critical: 4
        };

        var analyticsCfg = ((aiConfig.extensionConfig || {}).ApplicationInsightsAnalytics || {});
        if (!(aiConfig[strDisableExceptionTracking] === true || analyticsCfg[strDisableExceptionTracking] === true)) {
            var method = "onerror";
            _createMethods(["_" + method]);
            var originalOnError = win[method];
            win[method] = function (message, url, lineNumber, columnNumber, error) {
                var handled = originalOnError && originalOnError(message, url, lineNumber, columnNumber, error);
                if (handled !== true) {
                    appInsights["_" + method]({
                        message: message,
                        url: url,
                        lineNumber: lineNumber,
                        columnNumber: columnNumber,
                        error: error,
                        evt: win.event
                    });
                }

                return handled;
            };
            aiConfig.autoExceptionInstrumented = true;
        }

        return appInsights;
    })(snipConfig.cfg);

    win[aiName] = aiSdk;

    function _onInit() {
        if (snipConfig.onInit) {
            snipConfig.onInit(aiSdk);
        }
    }

    if (aiSdk.queue && aiSdk.queue.length === 0) {
        aiSdk.queue.push(_onInit);
        aiSdk.trackPageView({});
    } else {
        _onInit();
    }
}

// Called once per MFE from its own Program.cs (mirrors the backend's
// builder.AddAzureSuiteLogging(serviceName) — each app names itself here too).
function initAppInsights(connectionString, roleName) {
    if (!connectionString) {
        return;
    }

    loadApplicationInsightsSnippet(window, document, {
        src: "https://js.monitor.azure.com/scripts/b/ai.3.gbl.min.js",
        crossOrigin: "anonymous",
        onInit: function (sdk) {
            sdk.addTelemetryInitializer(function (envelope) {
                envelope.tags = envelope.tags || {};
                envelope.tags["ai.cloud.role"] = roleName;
            });
        },
        cfg: {
            connectionString: connectionString
        }
    });
}
```

- [ ] **Step 2: Link the script from `index.html`**

In `frontends/Catalog.Web/wwwroot/index.html`, add one line to `<head>` after the `theme.css`
link:

```html
    <script src="_content/AzureSuite.Web.UI/applicationInsights.js"></script>
```

- [ ] **Step 3: Add the base `appsettings.json` (new, committed)**

Create `frontends/Catalog.Web/wwwroot/appsettings.json`:

```json
{
  "ApplicationInsightsConnectionString": ""
}
```

(`CatalogApiBaseUrl` is deliberately not added here — it stays only in
`appsettings.Development.json` as today, so `Program.cs`'s existing
`?? "https://localhost:7184"` fallback keeps working: an empty string here would defeat that
fallback since `??` only replaces `null`, not `""`.)

- [ ] **Step 4: Add the placeholder key to `appsettings.Development.json`**

`frontends/Catalog.Web/wwwroot/appsettings.Development.json` becomes:

```json
{
  "CatalogApiBaseUrl": "https://localhost:7184",
  "ApplicationInsightsConnectionString": ""
}
```

- [ ] **Step 5: Call `initAppInsights` from `Program.cs`**

Update `frontends/Catalog.Web/Program.cs`:

```csharp
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace AzureSuite.Catalog.Web
{
    /// <summary>Entry point that bootstraps the Catalog.Web Blazor WebAssembly host.</summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.RegisterCustomElement<App>("catalog-app");

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(builder.Configuration["CatalogApiBaseUrl"] ?? "https://localhost:7184")
            });
            builder.Services.AddScoped<CatalogApiClient>();

            var host = builder.Build();

            var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
            await jsRuntime.InvokeVoidAsync("initAppInsights", builder.Configuration["ApplicationInsightsConnectionString"], "Catalog.Web");

            await host.RunAsync();
        }
    }
}
```

- [ ] **Step 6: Build to confirm it compiles**

Run: `dotnet build frontends/Catalog.Web/Catalog.Web.csproj`
Expected: `Build succeeded`, 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add frontends/AzureSuite.Web.UI/wwwroot/applicationInsights.js frontends/Catalog.Web/wwwroot/index.html frontends/Catalog.Web/wwwroot/appsettings.json frontends/Catalog.Web/wwwroot/appsettings.Development.json frontends/Catalog.Web/Program.cs
git commit -m "$(cat <<'EOF'
Wire Catalog.Web to Application Insights via shared JS loader

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Full solution verification

**Files:** none (verification only).

- [ ] **Step 1: Build and test the whole solution**

Run: `dotnet build AzureSuite.slnx` — expect `Build succeeded`, 0 warnings.
Run: `dotnet test AzureSuite.slnx` — expect every test project passing, 0 failures.

- [ ] **Step 2: Verify the local Event Log sink**

With `services/Catalog/Catalog.Api/appsettings.Development.json`'s `Serilog:WriteTo` still
containing the `EventLog` entry from Task 3, run `dotnet run --project services/Catalog/Catalog.Api`,
hit any endpoint (e.g. `GET /message-types`), then open Windows Event Viewer →
**Windows Logs → Application**, filter by source `Catalog.Api`, and confirm at least one log
entry appears (Serilog's default request-logging middleware isn't added here, but any
`ILogger` output from ASP.NET Core's own startup — e.g. "Now listening on..." — flows through
Serilog once `UseSerilog()` is active, or add a temporary `app.Logger.LogInformation("Catalog.Api started")`
right after `var app = builder.Build();` to confirm, then remove it).

- [ ] **Step 3: Verify Application Insights (both apps)**

Fill in the real connection string from Task 4 into both
`services/Catalog/Catalog.Api/appsettings.Development.json` and
`frontends/Catalog.Web/wwwroot/appsettings.Development.json` (if not already done in Task 4).
Run `dotnet run --project services/Catalog/Catalog.Api` and separately
`dotnet run --project frontends/Catalog.Web`, exercise both apps (hit the API, load the web
app in a browser), then open the shared Application Insights resource in the Azure Portal →
**Live Metrics** (fastest feedback) or **Logs** and confirm telemetry arrives tagged with
`cloud_RoleName` of `Catalog.Api` and `Catalog.Web` respectively — this is the concrete proof
that "each application initializes with its name" works end to end.

- [ ] **Step 4: Remove any temporary debug logging added during Step 2, then commit if anything changed**

If Step 2 required a temporary log line to confirm Event Log output, remove it now. If no
files changed as a result of this task, there's nothing to commit — this task is verification
only.
