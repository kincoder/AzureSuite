# Shared Observability (Serilog + Application Insights) — Design

## Why this piece, now

Per explicit direction: get error logging/observability wired up early, before more
services and MFEs pile up, rather than retrofitting it later. This is the first
cross-cutting infrastructure piece in the solution (following the same "shared library"
pattern already established by `AzureSuite.Web.UI` for frontend theming).

## Environments

Two environments, both active at once (not environment-gated on/off, just different
destinations):

- **Local** (`dotnet run` on a developer machine): logs also go to the Windows Event Log,
  so a developer can inspect them with Event Viewer without needing network access to
  Azure.
- **Azure**: logs go to a shared Application Insights resource. This is always configured
  (the connection string is present in every environment's config where available) — it
  is not "Azure-only", a developer running locally also sends telemetry to Application
  Insights if a connection string is configured, matching real usage in production
  troubleshooting during local repro sessions.

Concretely: Application Insights sink is added whenever a connection string is present in
config (any environment). The Windows Event Log sink is added only when
`IsDevelopment()` is true and the process is running on Windows — it is a local
developer convenience, never wired into a deployed Azure host.

## Backend services: `AzureSuite.Observability`

A new class library, `services/Shared/AzureSuite.Observability` (net10.0, plain
`Microsoft.NET.Sdk`, mirrors the "shared library referenced by every service" pattern of
`AzureSuite.Web.UI`, but lives under `services/Shared` rather than `frontends` since
backend services are the consumers).

**Packages:** `Serilog.AspNetCore` (10.0.0), `Serilog.Sinks.ApplicationInsights` (5.0.1),
`Serilog.Sinks.EventLog` (4.0.0), `Microsoft.ApplicationInsights` (3.1.2, for
`TelemetryConfiguration`/`ITelemetryInitializer` types the App Insights sink needs).

**Public surface:** one extension method,
`WebApplicationBuilder AddAzureSuiteLogging(this WebApplicationBuilder builder, string serviceName)`,
called once from each service's `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddAzureSuiteLogging("Catalog.Api");
```

Behavior:
1. Reads `ApplicationInsights:ConnectionString` from configuration.
2. Builds a Serilog `LoggerConfiguration` enriched with a `Service` property set to
   `serviceName` (useful for querying/filtering in Application Insights logs directly, in
   addition to the cloud role name mechanism below).
3. If the connection string is present: adds the Application Insights sink, using a
   `TelemetryConfiguration` whose `ConnectionString` is set and which has a custom
   `ITelemetryInitializer` (`CloudRoleNameTelemetryInitializer`) that sets
   `telemetry.Context.Cloud.RoleName = serviceName`. This is what lets every service log
   into the *same* Application Insights resource while staying distinguishable in the
   Application Map, Live Metrics, and log queries (`cloud_RoleName == "Catalog.Api"`) —
   this is the mechanism behind "each application initializes with its name."
4. If `builder.Environment.IsDevelopment()` and `OperatingSystem.IsWindows()`: adds the
   Event Log sink (`Serilog.Sinks.EventLog`), using `serviceName` as the event source
   (`manageEventSource: true` so it self-registers instead of requiring elevated setup).
5. Sets `Log.Logger` to the built configuration and calls `builder.Host.UseSerilog()`.

No connection string configured (e.g. a fresh clone with no `appsettings.Development.json`
override) → the app still runs, it just has no Application Insights sink; this matches
the existing "falls back gracefully" pattern already used for `CatalogDb` (InMemory
fallback when no connection string).

**`Catalog.Api` changes:** add the `AzureSuite.Observability` project reference, call
`builder.AddAzureSuiteLogging("Catalog.Api")` right after `WebApplication.CreateBuilder`,
add an empty `"ApplicationInsights": { "ConnectionString": "" }` placeholder to
`appsettings.json` (documents the key exists) and the real value goes into
`appsettings.Development.json` (gitignored value — see Secrets section) for local runs
against the shared Azure resource.

## Frontend MFEs: browser-side Application Insights

Blazor WebAssembly has no OS process — Windows Event Log does not apply. Local dev
visibility there is the browser console, which the Application Insights JavaScript SDK
already writes to on init/error. So for MFEs there is only one destination
(Application Insights), always active when a connection string is configured, matching
the "always send when configured" rule from the Environments section above.

**Shared piece — `AzureSuite.Web.UI`:** add
`frontends/AzureSuite.Web.UI/wwwroot/applicationInsights.js`, a small script exposing one
function:

```js
function initAppInsights(connectionString, roleName) {
    if (!connectionString) return;
    // Official Application Insights JS SDK loader snippet (lazy-loads the real SDK),
    // configured with the connection string, then tags every event with roleName via
    // a telemetry initializer so it's distinguishable in the same shared resource,
    // matching the backend's cloud role name convention.
}
```

**Per-MFE wiring — `Catalog.Web`:** `index.html` links `applicationInsights.js` (same
`_content/AzureSuite.Web.UI/...` static-asset pattern already used for `theme.css`) and
adds a small inline script that fetches the MFE's own `appsettings.json` /
`appsettings.Development.json` (the same file Blazor's `WebAssemblyHostBuilder` already
loads for `CatalogApiBaseUrl`) and calls
`initAppInsights(config.ApplicationInsightsConnectionString, "Catalog.Web")`. The role
name (`"Catalog.Web"`) is hardcoded per-MFE in that inline script, the same way each
backend service hardcodes its own name in its `Program.cs` call — consistent with "each
application initializes with its name."

## Azure infrastructure

New Bicep module, `infra/modules/shared/appinsights.bicep` (workspace-based Application
Insights, since the classic model is deprecated):

- A `Microsoft.OperationalInsights/workspaces` (Log Analytics) resource,
  `law-messaginghub-dev`, `PerGB2018` SKU, 30-day retention.
- A `Microsoft.Insights/components` (Application Insights) resource,
  `appi-messaginghub-dev`, `kind: 'web'`, `IngestionMode: 'LogAnalytics'`, pointed at the
  workspace above.
- Output: `connectionString` (the Application Insights connection string every
  service/MFE config points to).

Wired into `infra/main.bicep` as a new top-level module (not nested under `modules/catalog/`
like the existing Catalog-specific modules, since this resource is shared across every
current and future service) — `sharedAppInsights`, alongside the existing
`catalogKeyVault`/`catalogSql`/`catalogStaticWebApp` modules, with its `connectionString`
output surfaced as a new `main.bicep` output.

**Cost:** Application Insights + Log Analytics both have a free monthly ingestion
allowance (5 GB/month as of this writing) — comfortably enough for this project's current
traffic. This is "free as long as usage stays under that allowance," not free
unconditionally; if this project's log volume ever grows enough to matter, that's a
future cost conversation, not a blocker now.

## Secrets / config handling

The Application Insights connection string is not a credential in the traditional sense
(it doesn't grant write-then-read access to anything sensitive beyond telemetry
ingestion), but it shouldn't be committed to git either. Handling, consistent with how
`CatalogApiBaseUrl` already works for Catalog.Web:

- `appsettings.json` / `wwwroot/appsettings.json` (committed): empty placeholder key,
  documents the config shape.
- `appsettings.Development.json` / `wwwroot/appsettings.Development.json`: real value,
  **not committed** — added to `.gitignore` if not already covered, developer fills it in
  locally after the Bicep deployment prints the connection string once.
- Deployed Azure hosts (when Catalog.Api itself is eventually deployed as an App Service):
  the connection string is set as an App Service application setting sourced from the
  Bicep output, not from a committed file — out of scope for this piece since Catalog.Api
  isn't deployed publicly yet (per existing progress-log notes), but the shape above
  already supports it without changes when that happens.

## Out of scope

- Deploying Catalog.Api itself to Azure (unrelated, already tracked as separate future
  work per `docs/progress-log.md`).
- Structured log queries / alerts / dashboards in Application Insights — this piece is
  "logs flow to the right place, tagged correctly," not "build the observability
  dashboard."
- Distributed tracing / correlation between Catalog.Web and Catalog.Api calls — Serilog +
  the App Insights sink give basic request/exception telemetry per service; W3C trace
  context propagation across the browser→API boundary is a reasonable future enhancement,
  not required here.
- Log levels / sampling tuning — default Serilog minimum level (Information) and default
  Application Insights sink behavior are used as-is; can be revisited once real usage
  patterns are seen.

## Testing

`AzureSuite.Observability`'s `AddAzureSuiteLogging` is mostly composition of third-party
libraries with little independent branching logic; its test coverage (in a new
`tests/AzureSuite.Observability.Tests` project, matching this repo's 1:1 convention)
focuses on the one piece of custom logic: `CloudRoleNameTelemetryInitializer` correctly
sets `telemetry.Context.Cloud.RoleName`. The Windows-only Event Log branch and the
connection-string-present/absent branching are integration-shaped (they depend on OS and
external config) and are verified manually instead (steps included in the implementation
plan), not unit tested.
