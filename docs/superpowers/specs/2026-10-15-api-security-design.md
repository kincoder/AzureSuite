# API Security — Design

## Why

`Catalog.Api` and `Ingestion.Api` accept anonymous requests on Azure. Anyone with the URL can
submit messages (each one costs a Function run and a Cosmos write) or create clients and routes,
which means a stranger could authorize their own client to send. Auth was deferred in the master
spec (`2026-09-11-financial-messaging-hub-design.md`) until the pipeline worked. Now that it does,
this increment closes both holes.

## Goals

- No anonymous caller can reach any business endpoint on either API.
- Every authorization decision is **role based**, never just "is signed in". The owner holds every
  role, but each endpoint requires a specific one.
- Roles are defined as code (Bicep) without giving the CI principal any Microsoft Graph permissions.
- Operators sign in to the Blazor app. Producers (machines) authenticate with client credentials.
  Both reach Ingestion through the same role.

## Non-goals

- Processing (unchanged until #24, when its managed identity gets `Catalog.Reader`)
- Rate limiting per producer (follow-up issue)
- Running the APIs as `Production` (#25, a separate PR)
- Hiding the Static Web App's files from anonymous visitors (needs SWA Standard; a `later` issue).
  The SPA's files are public by nature and contain no secrets or data. The **APIs are the
  security boundary**; sign-in and role checks in the UI exist only for user experience.
- Security groups. Assigning a group to an app role needs Entra ID P1, so roles go directly to
  users.

## Roles

App roles are defined on the **API** registration. That's the resource that enforces them, and
roles only appear in access tokens issued for that resource. Every caller that matters for
security (an operator through the SPA, a producer, Processing later) holds an API access token, so
one definition and one assignment cover them all.

| Role (`value`) | Fixed id | Member types | Grants |
|---|---|---|---|
| `Catalog.Reader` | `d9abc6ed-af5a-45b4-ade8-786deaf412c0` | User, Application | List/get message types, clients, routes; `validate`; `routes/lookup` |
| `Catalog.Admin` | `7af834bf-b716-45f6-b286-ce6689f6b3a6` | User | Create/update/delete message types, clients, routes |
| `Messages.Submit` | `0eccf6ce-9b0e-4502-964d-cb5e95fe9444` | User, Application | `POST /messages` |

Delegated scope on the API: `access_as_user`, fixed id `9c7d0b32-c8f8-429b-a554-aa96cb0f8d97`.
Naming convention for future roles: `<Area>.<Permission>`.

## Identity infrastructure (`infra/identity/`)

A separate Bicep entry point using the **Microsoft Graph Bicep extension** (v1.0, dynamic types,
configured in `infra/identity/bicepconfig.json` so it cannot affect `main.bicep`). `main.bicep` doesn't reference it. The owner deploys it
manually with their own `az login`; it isn't run by CI.

```powershell
az deployment group create -g rg-messaginghub-dev `
  -f infra/identity/main.bicep -p infra/identity/identity.dev.bicepparam
```

`deploy-infra.yml` gets `paths-ignore: ['infra/identity/**']`, so edits never trigger CI.

| Resource | Details |
|---|---|
| `MessagingHub-Api` application + service principal | `uniqueName: messaginghub-api`; identifier URI `api://<appId>`; the three app roles; `access_as_user` scope; `requestedAccessTokenVersion: 2` |
| `MessagingHub-Web` application + service principal | `uniqueName: messaginghub-web`; `spa.redirectUris`: SWA URL + `https://localhost:7206` and `http://localhost:5147`, each with `/authentication/login-callback`; `requiredResourceAccess` → API `access_as_user` |
| `MessagingHub-TestProducer` application + service principal | `uniqueName: messaginghub-testproducer`; no credentials in Bicep |
| `oauth2PermissionGrants` | Web → API `access_as_user`, `consentType: AllPrincipals` (no consent prompt) |
| `appRoleAssignedTo` × 4 | Owner (`principalId` param) → all three roles; TestProducer → `Messages.Submit` |

Outputs: `apiClientId`, `apiScope` (`api://<appId>/access_as_user`), `webClientId`, `tenantId`.

**Known limitations, accepted:**
- Graph Bicep can't create client secrets. The producer secret is created once with
  `az ad app credential reset --id <producerAppId>` and kept locally, so it never appears in
  deployment history.
- No `what-if` for Graph resources.
- Replication delay: a role assignment right after its service principal is created can fail.
  Re-running the deployment fixes it.
- Drift: Portal edits aren't detected. Re-running the file restores the declared state.

## Shared authorization: `services/Shared/AzureSuite.Security`

A new class library that depends only on `Microsoft.AspNetCore.Authorization`, so it works both
server-side and in Blazor WebAssembly. Referenced by both APIs and the web app. No role string is
duplicated anywhere else.

- `AppRoles`: constants `CatalogReader`, `CatalogAdmin`, `MessagesSubmit`
- `AppPolicies`: constants `CatalogRead`, `CatalogWrite`, `SubmitMessages`
- `AuthorizationOptionsExtensions.AddAzureSuitePolicies(this AuthorizationOptions)`:
  - `CatalogRead` → `RequireRole(CatalogReader, CatalogAdmin)` (either)
  - `CatalogWrite` → `RequireRole(CatalogAdmin)`
  - `SubmitMessages` → `RequireRole(MessagesSubmit)`

Test project: `tests/AzureSuite.Security.Tests`.

## APIs

Both `Catalog.Api` and `Ingestion.Api`:

- Package `Microsoft.Identity.Web`. `builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration)`.
- `appsettings.json` → `AzureAd { Instance, TenantId, ClientId, Audience }`. These aren't secrets
  and are committed.
- `AddAuthorization(o => { o.AddAzureSuitePolicies(); o.FallbackPolicy = RequireAuthenticatedUser; })`,
  so the default is deny.
- `app.UseAuthentication(); app.UseAuthorization();` after `UseCors()`.
- Anonymous only: `MapAzureSuiteHealthChecks()`, `MapOpenApi()`, `MapScalarApiReference()`, each
  with `.AllowAnonymous()`.
- Endpoint → policy:

| Service | Endpoints | Policy |
|---|---|---|
| Catalog | `GET /message-types*`, `POST /message-types/{n}/{v}/validate`, `GET /clients*`, `GET /routes*` (incl. `lookup`) | `CatalogRead` |
| Catalog | `POST /message-types`, `POST/PUT/DELETE /clients*`, `POST/PUT/DELETE /routes*` | `CatalogWrite` |
| Ingestion | `POST /messages` | `SubmitMessages` |

- Responses: no token or an invalid one → 401; valid token without the role → 403. Handlers are
  unchanged.
- The shared `AddMicrosoftIdentityWebApiAuthentication` wiring stays inline in each `Program.cs`
  (one line). It doesn't go in a shared library, because that would pull server-only dependencies
  into a project the WASM app references.

## Web app (`AzureSuite.Web.Blazor`)

- Package `Microsoft.Authentication.WebAssembly.Msal`. `AddMsalAuthentication` binds `AzureAd`
  (`Authority`, `ClientId`, `ValidateAuthority`) and requests `ApiScope` by default.
- Both typed clients (`CatalogApiClient`, `IngestionApiClient`) get an `AuthorizationMessageHandler`
  configured with `authorizedUrls` = the two API base URLs and `scopes` = `ApiScope`.
- **Roles in the UI:** the roles are in the API access token, not in the SPA's ID token.
  `ApiRolesClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>` requests the
  API access token, reads its `roles` claim (payload decode only, no validation) and adds a
  `ClaimTypes.Role` claim for each role. It's used for presentation only.
- `AddAuthorizationCore(o => o.AddAzureSuitePolicies())`.
- `App.razor` → `CascadingAuthenticationState` + `AuthorizeRouteView`; unauthenticated users are
  redirected to sign-in (`RedirectToLogin` component).
- `Pages/Authentication.razor` at `/authentication/{action}` (`RemoteAuthenticatorView`).
  `staticwebapp.config.json`'s existing `navigationFallback` already serves it.
- `Layout/LoginDisplay` in the nav: the user's name and a sign-out link.
- Per-role UI:
  - Pages: list pages `[Authorize(Policy = CatalogRead)]`, register pages `CatalogWrite`,
    `SubmitMessage` `SubmitMessages`.
  - `NavMenu` links and the list pages' "Register" buttons are wrapped in `<AuthorizeView Policy=…>`.
  - A signed-in user without the needed role sees a "You don't have access" state (an `ErrorState`
    variant), not a blank page.
- Config: `wwwroot/appsettings.json` gains `AzureAd.Authority`
  (`https://login.microsoftonline.com/__AZURE_AD_TENANT_ID__`), `AzureAd.ClientId`
  (`__AZURE_AD_WEB_CLIENT_ID__`), `ApiScope` (`__API_SCOPE__`). `appsettings.Development.json`
  gets the real values; none of them are secrets. `deploy-web` substitutes three new `azure-dev`
  variables: `AZURE_AD_TENANT_ID`, `AZURE_AD_WEB_CLIENT_ID`, `API_SCOPE`.

## Testing

- **`AzureSuite.Security.Tests`**: for each policy, `IAuthorizationService` against principals with
  each role, no role, and the wrong role. Covers the reader-or-admin rule for `CatalogRead`.
- **API tests** (`CatalogApiFactory`, `IngestionApiFactory`): replace JWT bearer with a
  `TestAuthHandler` scheme. The test header `X-Test-Roles: Catalog.Admin,…` authenticates the
  caller with those roles; no header means unauthenticated. New tests per API:
  - anonymous → 401 on a business endpoint
  - wrong role → 403 (e.g. `Catalog.Reader` on `POST /clients`, `Catalog.Admin` on `POST /messages`)
  - correct role → the existing success status
  - `/health` anonymous → 200
  Existing tests send the role they need through a helper on the factory.
- **Blazor tests** (bUnit `AddTestAuthorization()`): `NavMenu` shows or hides links per role; the
  list pages' Register button shows only with `Catalog.Admin`; `LoginDisplay` signed in and out.
  `ApiRolesClaimsPrincipalFactory` unit test with a fake access-token provider returning a token
  whose payload has `roles`.

## Rollout order

1. Owner deploys `infra/identity` manually, then `az ad app credential reset` for TestProducer.
2. Fill in the client IDs in the APIs' `appsettings.json`; set the three `azure-dev` variables.
3. PR → `develop` → `master`. Deploying makes both APIs require tokens, and the web app signs in.
4. Verify on Azure:
   - anonymous `curl POST /messages` → 401
   - TestProducer token → 201
   - signed in as owner → every page works
   - remove `Catalog.Admin` from the owner temporarily → Register buttons disappear and a direct
     `POST /clients` returns 403; then restore it

Docs: `docs/infrastructure.md` (identity deployment, limitations), `docs/services/*.md` (auth per
endpoint, local sign-in, how a producer gets a token).

## Deviations recorded during implementation

- **Identifier URI** is `api://<tenantId>/messaginghub-api`, not `api://<appId>`. A Bicep resource
  can't reference its own appId.
- **Every policy also requires an authenticated user.** `RequireRole` alone only checks
  `IsInRole`, so an unauthenticated identity carrying a role claim would pass.
- **Microsoft Graph grant:** the web app also gets a tenant-wide `openid profile offline_access`
  grant, so first sign-in shows no consent prompt.
- **`deploy-infra.yml`** excludes the folder with a `!infra/identity/**` pattern inside `paths`.
  GitHub doesn't allow `paths` and `paths-ignore` on the same event.
- **Patched crypto packages:** `Microsoft.Identity.Web` 4.16.1 and SqlClient bring
  `System.Security.Cryptography.Pkcs`/`Xml` versions with known vulnerabilities. Both APIs and
  `Catalog.Infrastructure` pin `Pkcs` 10.0.13; the API test projects pin `Xml` 10.0.13.
