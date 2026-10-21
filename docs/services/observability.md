# AzureSuite.Observability

**Where:** [services/Shared/AzureSuite.Observability/](../../services/Shared/AzureSuite.Observability/)

## What it solves

In a distributed flow (web → API → queue → function), one failure can involve several
services. Every service should log the same way to the same place, so a message can be traced
end to end. This shared library gives each service that setup in one line.

## What's in it

| Piece | What it does |
|---|---|
| `AddAzureSuiteLogging(serviceName)` | Sets up Serilog: reads sinks from config, enriches every event with `Service`, and writes to App Insights when `ApplicationInsights:ConnectionString` is set |
| `CloudRoleNameTelemetryInitializer` | Stamps `cloud_RoleName` on telemetry so each service shows up as its own node in App Insights |
| `MapAzureSuiteHealthChecks()` | `/health` (anonymous liveness, runs no checks) and `/health/verbose` (signed-in callers: each check's detail, filter with `?tags=data`) |

Everything sends to one shared Application Insights resource (`infra/modules/shared/appinsights.bicep`).
The Blazor app sends its own browser telemetry through `ClientTelemetryLogger`.

## Finding things in App Insights

- Logs by service: `traces | where cloud_RoleName == "<app name>"`
- Failures: `exceptions` plus the **Failures** blade
- Dependency map: **Application map**
