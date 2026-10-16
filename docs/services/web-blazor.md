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
The sign-in values (`AzureAd:Authority`, `AzureAd:ClientId`, `ApiScope`) follow the same
pattern: `__AZURE_AD_TENANT_ID__`, `__AZURE_AD_WEB_CLIENT_ID__` and `__API_SCOPE__` in
`appsettings.json`, filled in from the `azure-dev` variables of the same names. None of them are
secrets.

## Sign-in and roles

- **MSAL** (`Microsoft.Authentication.WebAssembly.Msal`) signs the user in with the
  `MessagingHub-Web` registration and requests the API scope. `ApiAuthorizationMessageHandler`
  attaches the token to the two API clients only (`ApiAccessOptions.AuthorizedUrls`).
- **Every page requires a policy** (`[Authorize(Policy = …)]`). Signed-out users are sent to
  sign-in (`RedirectToLogin`). Signed-in users without the role see "You don't have access".
  If the API token can't be renewed silently (an expired session), they're offered "Sign in
  again" instead, and an API call in that state redirects to sign-in
  (`ApiAuthorizationMessageHandler`).
- **Why `ApiRolesClaimsPrincipalFactory` exists:** app roles are defined on the *API*
  registration, so they're in the API access token, not in this app's ID token. The factory
  copies the `roles` claim from the API token onto the user, so `AuthorizeView` and
  `[Authorize]` use the same roles and policies (`AzureSuite.Security`) as the APIs.
- **The UI only decides what to show.** Nav links and Register buttons are hidden by
  `AuthorizeView`, but the APIs enforce every role regardless. The app's files are public, like
  any SPA's, and contain no secrets.

## Conventions

Components with non-trivial logic use code-behind (`X.razor` + `X.razor.cs` + `X.razor.css`).
Tests live in `tests/AzureSuite.Web.Blazor.Tests` and use bUnit.
