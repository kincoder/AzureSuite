# AzureSuite.Web.Blazor

**Where:** [frontends/AzureSuite.Web.Blazor/](../../frontends/AzureSuite.Web.Blazor/) ·
**Runs on:** Static Web App `stapp-messaginghub-web-dev` (Free tier) ·
**URL:** https://delightful-sky-05bade503.2.azurestaticapps.net

## What it solves

Operators need a UI to manage the hub's reference data and to send test messages, without
using curl or Scalar. This is a single Blazor WebAssembly app covering every service's
features.

> An earlier attempt split the UI into several micro-frontends (one WASM runtime each) on one
> page. It was abandoned because the runtimes' event delegators interfere with each other. See
> the 2026-09-23 pivot in [progress-log.md](../progress-log.md). An Angular version with real
> Module Federation is planned as a comparison.

## Structure

Vertical slices: each feature owns its pages, API client and contracts.

| Folder | Contents |
|---|---|
| `Features/Catalog/` | Pages for message types, clients and routes (list and register); `CatalogApiClient` |
| `Features/Ingestion/` | `SubmitMessage` page; `IngestionApiClient` |
| `Layout/` | `MainLayout`, `NavMenu`, `AppFooter` (links to each API's Scalar page), `AppErrorBoundary` (logs and recovers on navigation) |
| `UI/` | Shared components (`AppButton`, `DataTable`, `PageHeader`, `EmptyState`, `ErrorState`, `SkeletonRow`, `AppBadge`), `ClientTelemetryLogger` (browser → App Insights) |

## Configuration

`wwwroot/appsettings.json` contains placeholders (`__CATALOG_API_BASE_URL__` and so on). The
`deploy-web` job replaces them with values from the `azure-dev` GitHub environment.
`appsettings.Development.json` points at the local APIs (`https://localhost:7184` and `:7185`).

## Conventions

Components with non-trivial logic use code-behind (`X.razor` + `X.razor.cs` + `X.razor.css`).
Tests live in `tests/AzureSuite.Web.Blazor.Tests` and use bUnit.
