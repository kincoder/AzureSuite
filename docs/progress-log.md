# AzureSuite — Progress Log

This is a study-case C# solution to hands-on learn most of Azure via IaC (Bicep) + CI/CD
(GitHub Actions), built around a **financial messaging hub** domain.

## Pivot (2026-09-11): reset to an upfront end-to-end design

The original payments/PACS.008 scaffold (below, kept for history) worked end to end but
was built feature-by-feature without a full design, and the frontend direction
(micro-frontends) was adopted mid-build rather than decided upfront. Reset: all old
code/infra removed from the repo, `rg-azuresuite-dev` and its two Entra ID app
registrations (`AzureSuite-Api`, `AzureSuite-Web`) deleted from Azure. New direction is
fully specified first — see
`docs/superpowers/specs/2026-09-11-financial-messaging-hub-design.md` — before any
implementation resumes. Everything below this point describes the superseded build,
kept as reference for tooling/gotchas that likely still apply (EF Core setup,
DefaultAzureCredential slowness locally, Key Vault secret naming, etc.).

## Messaging Hub — Catalog service (2026-09-11): first service live end-to-end

Catalog (services/Catalog/) built via TDD per `docs/superpowers/plans/2026-09-11-catalog-service.md`:
Domain (MessageType entity + MessageTypeName/MessageTypeVersion value objects — real DDD,
not just folder separation), Application (lightweight CQRS via MediatR — RegisterMessageType
command, GetMessageType/ListMessageTypes queries), Infrastructure (EF Core, SQL Server),
Api (minimal API endpoints, Scalar UI at /scalar/v1 in Development). Deployed to Azure SQL
(`sql-messaginghub-catalog-dev`/`catalog` database) in `rg-messaginghub-dev` and verified
working end-to-end against the real database (register + get + list all round-trip
correctly). 31 tests passing across 4 test projects, 0 warnings.

**Coding conventions locked in during this build (apply to every future service too):**
block-scoped namespaces everywhere (no file-scoped `namespace X;`), no top-level statements
(every `Program.cs` has explicit `class Program` + `static void Main`), minimal API
endpoints (not `[ApiController]` classes), XML doc comments on every class and on any
non-obvious property, request DTOs live in their own `Contracts/` folder rather than inline
in `Program.cs`. See `docs/superpowers/plans/2026-09-11-catalog-service.md`'s Global
Constraints for the authoritative list.

**Two real bugs hit and fixed here (worth remembering):**
1. **EF Core silently dropped a column with no error.** `MessageType.RegisteredAtUtc` (a
   get-only auto-property, same shape as `Id`/`Name`/`Version`/`SchemaDefinition`) was
   missing entirely from the generated migration/table — no warning, just absent. The other
   four properties worked because each was explicitly touched somewhere in
   `OnModelCreating` (`HasKey`, `Property(...).HasConversion(...)`, or `Property(...).IsRequired()`);
   `RegisteredAtUtc` was the only one never referenced there. Fix: add
   `entity.Property(m => m.RegisteredAtUtc).IsRequired();` explicitly. Lesson: don't rely on
   pure convention-based discovery for get-only properties — touch every mapped property in
   `OnModelCreating` at least once, and manually inspect generated migrations for
   completeness before applying them (the InMemory-provider unit tests did **not** catch
   this, since InMemory doesn't generate a relational schema).
2. **`WebApplicationFactory` integration tests hit the real Azure SQL database** because the
   test host reused `Catalog.Api`'s configuration (including user secrets) unchanged, and
   `AddDbContext` had already been called with `UseSqlServer` once a real connection string
   was set locally. This silently wrote test data into the live dev database and made test
   runs non-repeatable (second run failed with "already registered"). Fixed with a custom
   `CatalogApiFactory : WebApplicationFactory<Program>` that removes **both**
   `DbContextOptions<CatalogDbContext>` **and** `IDbContextOptionsConfiguration<CatalogDbContext>`
   (the latter is additive/enumerable — removing only the former left both SqlServer's and
   InMemory's provider configuration applied to the same options, causing "only a single
   database provider can be registered") before re-registering `UseInMemoryDatabase`. Also
   hit: computing the InMemory database name via `Guid.NewGuid()` **inside** the
   `AddDbContext` configure lambda gives every request its own empty database, since that
   lambda runs per-DbContext-construction (i.e. per request) — the Guid must be computed
   once outside the lambda and captured. Lesson: any integration test project using
   `WebApplicationFactory` against a project with a real external connection string needs an
   explicit override like this from day one, not discovered after the fact.

## Messaging Hub — infra naming (2026-09-11)

New resource group for the rebuild: `rg-messaginghub-dev` (West Europe, matching the
prior project's region convention). Per-service naming pattern:
`<type>-messaginghub-<service>-dev`, e.g. `sql-messaginghub-catalog-dev`. Key Vault
shortened to `kv-msghub-<service>-dev` (24-char limit), e.g. `kv-msghub-catalog-dev`.
Each service gets its own SQL server + Key Vault under this one resource group —
services stay logically separate (own DB, own secrets) without needing a resource
group per service. Follow this same pattern for Ingestion, Routing, Delivery, Archive,
Monitoring as they're built.

## Milestone (2026-09-02): end-to-end flow working (superseded, see pivot above)

Sign in on the Blazor Web UI (Entra ID) → submit a PACS.008 message via the form →
Web calls the API with an acquired token → API validates it, runs it through the
Application/Infrastructure layers → persisted to Azure SQL → shows up in the list on
reload. All pieces (SSO, JWT-secured API, DDD layering, EF Core, IaC-provisioned
resources) are connected and verified working together for the first time here.
35 unit tests passing. Next phase moves into messaging (Service Bus/Functions)
and observability (App Insights) rather than further hardening this slice.

Read this file at the start of a new session to recover context without replaying the
whole conversation. Update it after each meaningful step (decision made, resource created,
file added) — keep entries short and factual, not a transcript.

## Code quality & testing rules (non-negotiable)

- Hand-written code must be warning-free, nullable-enabled, no `#pragma warning disable`
  suppressions. Exception: EF Core migration `*.Designer.cs` / `*ModelSnapshot.cs` files —
  these are tool-generated, always carry `#nullable disable` and
  `#pragma warning disable 612, 618` by EF's own convention, and must never be hand-edited.
  Do not "fix" those files; only regenerate them via `dotnet ef migrations add/remove`.
- Every code file should be clean and organized; comment only non-obvious *why*, not *what*.
- **Blazor components with non-trivial logic use code-behind** (`Component.razor` +
  `Component.razor.cs` as a partial class), matching the existing scoped-CSS split
  (`Component.razor.css`). Small components (a handful of lines) can stay inline in
  `@code { }`. Don't put `@inject` in both the `.razor` file and the code-behind for the
  same service — pick one (prefer `[Inject]` property in code-behind) or it won't compile
  (`CS0102: already contains a definition`).
- **Don't duplicate DTOs/request shapes between the API and UI.** If both need the same
  shape (e.g. a create-request), share the actual Application-layer type rather than
  hand-rolling a shadow "ViewModel" class. Watch for the record gotcha: a `record` with
  positional constructor parameters defaults to init-only properties, which Blazor's
  `EditForm`/`@bind-Value` cannot assign to - if a shared request type needs two-way form
  binding, make it a plain class with `{ get; set; }` properties, not a record (see
  `CreatePacs008MessageRequest`).
- **Testing convention**: for each `src/X.Y` project, there is a matching `tests/X.Y.Tests`
  project (xUnit + FluentAssertions + coverlet.collector for coverage). The test project's
  folder structure mirrors the source project's 1:1 (e.g. `Entities/User.cs` →
  `Entities/UserTests.cs`), one test class per production class, aiming for high coverage.
  Composition-root files (`Program.cs`) are exempt from this per-class rule — they get
  covered later via integration tests (e.g. `WebApplicationFactory`), not unit tests.
  Run `dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults` to
  generate `coverage.cobertura.xml` reports (gitignored).

## Ground rules for this project

- One step at a time. Explain the *why* behind Azure setup, not just paste code —
  the user wants to learn the Portal and the concepts, not just get a working repo.
- Prefer Bicep (IaC) over manual Portal clicks for anything repeatable, but the Portal
  is still used deliberately as a learning tool (e.g. the resource group was created
  manually on purpose).
- Never commit secrets. Passwords/keys go into Key Vault; Bicep pulls them via
  `az.getSecret()` in `.bicepparam` files, never as plaintext parameters.
- One `.bicepparam` file per environment (e.g. `main.dev.bicepparam`), so resource
  names/config are written once and reused, not retyped per deploy.
- C# solution, .NET 10 SDK.

## Decisions made

- UI: Blazor Server (not React) — one language, simpler to wire SSO into.
- CI/CD: GitHub Actions (not Azure DevOps).
- IaC: Bicep (not Terraform) — no state file to manage, tighter Azure/Portal correspondence,
  better fit for an Azure-only learning project. Terraform deferred as a "second pass" topic.
- Repo: private, https://github.com/kincoder/AzureSuite
- Azure subscription: Pay-As-You-Go, id `2008745c-a136-400d-8f66-7c9f5fe0939f`,
  tenant `46b33134-1b34-42b4-9848-6b40016d2e22` (requires MFA — use
  `az login --tenant 46b33134-1b34-42b4-9848-6b40016d2e22 --use-device-code`).
- Region for all resources: **West Europe** (matches the resource group).
- Naming pattern: `<type>-azuresuite-dev-pumpkin` (Key Vault shortened to `kv-azsuite-...`
  because of the 24-char limit).

## Solution structure (as scaffolded)

```
AzureSuite.slnx
src/
  AzureSuite.Api/            → Web API — will validate Entra ID JWTs (no app-owned auth) — not yet wired
  AzureSuite.Web/            → Blazor Server UI (SSO) — not yet implemented
  AzureSuite.Functions/      → isolated-worker Azure Functions — not yet implemented
  AzureSuite.Domain/         → Entities/Pacs008Message.cs, Enums/MessageStatus.cs, ValueObjects/PartyAccount.cs
  AzureSuite.Application/    → use cases/interfaces — empty scaffold so far
  AzureSuite.Infrastructure/ → Persistence/AppDbContext.cs (EF Core, SQL Server) + Migrations/
tests/
  AzureSuite.Domain.Tests/         → mirrors AzureSuite.Domain (xUnit + FluentAssertions)
  AzureSuite.Infrastructure.Tests/ → mirrors AzureSuite.Infrastructure (+ EF Core InMemory provider)
infra/
  main.bicep                 → orchestrator, resource-group scoped
  main.dev.bicepparam        → dev environment's actual parameter values (committed;
                                pulls secrets live via az.getSecret(), never stores them)
  modules/sql.bicep          → Azure SQL server + serverless free-tier database
  modules/keyvault.bicep     → Key Vault, RBAC role assignment, sql-admin-password secret
docs/
  progress-log.md            → this file
.vscode/
  extensions.json            → recommended extensions (C# Dev Kit, Azure Tools, Bicep, etc.)
```

Project references: Domain ← Application ← Infrastructure ← {Api, Functions}. Web currently
stands alone (SSO wiring will connect it to Entra ID directly, not through Application layer).

## Azure resources created so far

All in resource group **rg-azuresuite-dev** (West Europe), created manually via Portal.

| Resource | Name | Notes |
|---|---|---|
| Resource Group | `rg-azuresuite-dev` | created via Portal, deliberately, as a learning step |
| SQL Server (logical) | `sql-azuresuite-dev-pumpkin` | admin login `sqladmin`, password rotated & stored in Key Vault |
| SQL Database | `azuresuite` | serverless GP_S_Gen5, `useFreeLimit: true` (100k vCore-s/mo free), auto-pauses after 60 min idle |
| Firewall rule | `AllowAzureServices` | 0.0.0.0-0.0.0.0 special range, allows Azure-hosted services through |
| Key Vault | `kv-azsuite-dev-pumpkin` | RBAC-based auth (not legacy access policies); user has "Key Vault Secrets Officer" role |
| Key Vault secret | `sql-admin-password` | rotated once already; source of truth for the SQL password going forward |

Not yet created: Cosmos DB (Mongo API) for logs, Service Bus, Azure Functions app,
App Service (API + Web), Application Insights / Log Analytics, Entra ID app registration for SSO.

## RBAC concepts covered (for reference)

- A **role assignment** (principal + role definition + scope) is a child object of whatever
  it's scoped to (e.g. our Key Vault role assignment lives under the vault; delete the vault,
  the assignment goes too). A **role definition** (e.g. "Key Vault Secrets Officer",
  guid `b86a8fe4-44ce-4948-aee5-eccb2c155cd7`) is tenant-wide for built-ins — Microsoft ships
  the same definitions everywhere; it's referenced by assignments, not owned by any resource.
- Central place to view assignments: Portal → any scope → **Access control (IAM)** →
  Role assignments tab (also lists ones inherited from higher scopes). Role *definitions*
  (all built-ins + custom) are under the **Roles** tab there. Entra ID (its own Portal blade)
  manages identities themselves (users/groups/app registrations) — a related but separate
  system from RBAC, which consumes those identities.
- SQL Server has **two separate access layers**: Azure RBAC on `Microsoft.Sql/servers`
  (management plane — firewall rules, scaling, etc.) vs. data-plane login to actually query
  data (SQL auth username/password, or Entra ID auth). These don't overlap.
- `az.getSecret()` in a `.bicepparam` file is resolved by **Azure Resource Manager's own
  first-party service principal** at deployment time, not by the CLI user's own login —
  this requires the vault to have `enabledForTemplateDeployment: true`, a permission
  separate from any RBAC role granted to a human user. Hit this as
  `KeyVaultParameterReferenceSecretRetrieveFailed` on first deploy; fixed by adding that
  property to `modules/keyvault.bicep`.

## SQL Entra ID (Azure AD) authentication

- Added `Microsoft.Sql/servers/administrators` (type `ActiveDirectory`) in
  `modules/sql.bicep`, pointing at the same Entra object id used for the Key Vault RBAC
  role. This runs **alongside** the existing `sqladmin` SQL-auth login, not replacing it.
- Test it: Portal → `azuresuite` database → Query editor (preview) → choose
  "Microsoft Entra authentication" instead of SQL login — no password needed.
- Not yet done: fully retiring SQL auth (`azureADOnlyAuthentication`), and wiring
  app/Functions managed identities as additional Entra SQL users — planned for when
  the API/Functions are deployed to Azure (managed identity is the passwordless pattern
  for service-to-service auth, covered when we get there).

## Tooling installed on this machine

- .NET 10 SDK
- GitHub CLI (`gh`) — authenticated as `kincoder`. Installed via winget to user scope
  (had to retry with `--silent --scope user` after the MSI GUI prompt failed non-interactively).
- Azure CLI (`az`) — authenticated (Pay-As-You-Go sub above). Installed via winget default
  (machine) scope, `--silent` needed since default interactive MSI failed non-interactively.
- Bicep CLI v0.46.1 — installed via `az bicep install`.
- Note: winget-installed CLIs land under `%LOCALAPPDATA%\Microsoft\WinGet\Packages\...`;
  PATH updates from winget need a fresh shell session (each tool invocation in this
  environment is a fresh process, so `$env:PATH` had to be rebuilt from Machine+User
  scope manually a few times).

## Deployment commands (reference)

```powershell
# refresh PATH in a fresh PowerShell process if az/gh/bicep aren't found
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")

# validate without applying
az deployment group what-if --resource-group rg-azuresuite-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam

# deploy dev
az deployment group create --resource-group rg-azuresuite-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```

## Open items / next steps

1. ~~EF Core + SQL~~ — done.
2. ~~Auth, revised direction~~ — done, see "Entra ID auth (done)" below.
3. ~~Domain pivot to PACS.008~~ — done, see below.
4. ~~DDD layering (repository + service)~~ — done, see below.
5. ~~Blazor Web UI: SSO, call API, list/create messages~~ — done, see
   "Blazor Web UI (done)" below. Milestone reached 2026-09-02: full flow verified working.
6. Mongo logging: start with local container, later Cosmos DB (Mongo API, free tier) —
   likely storing the raw message payload/audit trail, complementing the SQL record.
7. Service Bus + Functions wiring: API receives a message → publishes to Service Bus →
   Function consumes → persists to Mongo (audit) + SQL (reference/settlement data).
8. Application Insights / Log Analytics — trace the whole pipeline end to end.
9. GitHub Actions pipeline (deploy infra + apps).
10. Hardening (not urgent): retire SQL password entirely (`azureADOnlyAuthentication`),
    integration tests for `[Authorize]`/scopes via `WebApplicationFactory`.

## Entra ID auth (done)

- **App Registration**: `AzureSuite-Api`, single-tenant, no redirect URI (it's a resource
  server, not a client). Tenant `46b33134-1b34-42b4-9848-6b40016d2e22`, client ID
  `edd37b21-2fdb-401b-992b-96723111682a`.
- **Exposed API**: Application ID URI `api://edd37b21-2fdb-401b-992b-96723111682a`, scope
  `Messages.ReadWrite` (admins + users can consent).
- **Authorized client applications**: had to explicitly add Azure CLI's well-known client ID
  `04b07795-8ddb-461a-bbee-02f9e1bf7b46` under Expose an API, otherwise `az account
  get-access-token --resource api://...` fails with `AADSTS650057` (CLI's own registration
  doesn't declare arbitrary custom resources) even after consenting — pre-authorization is
  required for a public client like the CLI to request a custom API's scope at all.
- `AzureSuite.Api`: added `Microsoft.Identity.Web`, `AzureAd` config section in
  `appsettings.json` (Instance/TenantId/ClientId — not secrets, safe to commit),
  `AddAuthentication(Constants.Bearer).AddMicrosoftIdentityWebApi(...)` +
  `app.UseAuthentication()` in `Program.cs`. Added `MessagesController` with
  `[Authorize] [RequiredScope("Messages.ReadWrite")]` as the first protected endpoint.
- Verified end-to-end locally: no token → 401; valid token (acquired via
  `az account get-access-token --resource api://edd37b21-2fdb-401b-992b-96723111682a`
  after `az login --scope api://edd37b21-2fdb-401b-992b-96723111682a/.default`) → audience
  and signature validated against the tenant, query executed, 200 returned.
- Not yet done: `[Authorize]`/scope tests (integration-style, `WebApplicationFactory`);
  Blazor Web will likely need its own app registration (interactive/delegated flow) or
  reuse this one, decide when we build the UI.

## Architecture: proper layering (done, 2026-09-02)

User flagged `MessagesController` reading `AppDbContext` directly as an architectural
smell. Fixed with standard DDD/Clean Architecture layering:

- `AzureSuite.Application/Abstractions/IPacs008MessageRepository.cs` — persistence
  contract, defined in Application (depends only on Domain), implemented in
  Infrastructure. Classic Dependency Inversion.
- `AzureSuite.Application/Messages/` — `Pacs008MessageService` (business logic +
  domain→DTO mapping), `IPacs008MessageService`, `Pacs008MessageSummaryDto` (output),
  `CreatePacs008MessageRequest` (input). Controllers/UI never see the Domain entity
  directly, only these DTOs.
- `AzureSuite.Infrastructure/Persistence/Repositories/Pacs008MessageRepository.cs` —
  the actual EF Core implementation.
- `MessagesController` now depends only on `IPacs008MessageService`; gained a
  `POST /api/messages` alongside the existing `GET`.
- Testing: `AzureSuite.Application.Tests` (new project) tests `Pacs008MessageService`
  against a hand-written `FakePacs008MessageRepository` (no mocking library needed for
  a 3-method interface) — keep using this pattern until interfaces get complex enough
  to justify NSubstitute/Moq. `AzureSuite.Infrastructure.Tests` gained
  `Pacs008MessageRepositoryTests` (EF Core InMemory). 15 tests total across 3 test
  projects, all passing.

## Blazor Web UI (done, 2026-09-02)

Built the UI to actually exercise the API (was previously curl/Scalar-only). Full flow
verified working end-to-end: sign in → submit message via form → list refreshes.

- **Separate Entra ID app registration** `AzureSuite-Web` (client id
  `c2f4de7f-9ffd-4e6f-93bf-ced2f2978da3`), redirect URI `https://localhost:7081/signin-oidc`,
  own client secret — deliberately separate from `AzureSuite-Api`: the API is a resource
  server (no redirect URI, validates tokens), the Web app is a client (signs users in,
  acquires tokens on their behalf). Client secret stored in Key Vault as `web-client-secret`
  (not `spn-azuresuite-web-secret`, renamed to match the `{resource}-{purpose}` convention
  — see naming convention note below), read via a custom `KeyVaultSecretManager` mapping
  explicit secret names to config keys rather than relying on Key Vault's `--`→`:`
  auto-mapping convention (which would force ugly secret names).
- **API Permissions**: delegated `Messages.ReadWrite` on `AzureSuite-Api`, admin consent
  granted tenant-wide.
- Packages: `Microsoft.Identity.Web`, `.UI` (sign-in/out endpoints,
  `/MicrosoftIdentity/Account/SignOut`), `.DownstreamApi` (`IDownstreamApi.CallApiForUserAsync`
  for calling the API with an acquired token).
- `Components/Pages/Messages.razor` — list + create form, calls the API via `IDownstreamApi`.
- `Components/Layout/UserMenu.razor` — avatar-circle-with-dropdown (name/email + sign out),
  replacing a plain inline text+link.
- `builder.Services.AddAuthorization(options => options.FallbackPolicy = ...RequireAuthenticatedUser())`
  — every page requires sign-in, no anonymous content in this app.

### Gotchas hit and fixed (worth remembering)

1. **`DefaultAzureCredential` is very slow locally** (10-20s+) — it probes Managed Identity's
   IMDS endpoint, which doesn't exist on a dev machine, before falling back through several
   more credential types to `AzureCliCredential`. Fixed by branching:
   `builder.Environment.IsDevelopment() ? new AzureCliCredential() : new DefaultAzureCredential()`
   — full chain (including Managed Identity) still used automatically once actually deployed.
2. **This Bash tool session's `PATH` doesn't auto-refresh** after installing CLIs via winget
   in a separate PowerShell call — `az` wasn't found when spawning `dotnet run` from Bash,
   causing `AzureCliCredential authentication failed: Azure CLI not installed` even though
   `az` works fine from PowerShell. Fixed by prepending the winget install path
   (`/c/Program Files/Microsoft SDKs/Azure/CLI2/wbin`) to `PATH` in the Bash session before
   running `dotnet run`.
3. **`DownstreamApiOptions.Scopes` binds to `List<string>`** — a plain JSON string in
   `appsettings.json` (`"Scopes": "api://.../Messages.ReadWrite"`) silently binds to `null`
   instead of erroring, so no token gets attached at all. Must be a JSON array:
   `"Scopes": [ "api://.../Messages.ReadWrite" ]`. Silent failure surfaced as
   `[MsIdWeb] An unauthenticated call was made to the Api with null Scopes` in the log,
   and a plain 401 with no other clue in the UI.
4. **`EnableTokenAcquisitionToCallDownstreamApi()` needs explicit initial scopes** — called
   with no arguments, it doesn't pull scopes from a later `AddDownstreamApi(...)` call, so
   the sign-in's authorization request never actually requests `Messages.ReadWrite`. Later,
   calling the API fails with `IDW10502 MsalUiRequiredException` because Blazor Server can't
   redirect mid-circuit for interactive re-consent. Fix: pass the scopes explicitly —
   `.EnableTokenAcquisitionToCallDownstreamApi(messagesApiScopes)`.
5. **Azure CLI needs explicit pre-authorization to request tokens for a custom API scope** —
   `az account get-access-token --resource api://...` fails with `AADSTS650057` even after
   granting admin consent, because the CLI's own app registration doesn't declare arbitrary
   custom resources. Fix: on the API's app registration → Expose an API → Authorized client
   applications → add Azure CLI's well-known client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46`
   with the relevant scope checked.
6. **Blazor Web App template defaults to *per-page* interactivity, not global** — pages
   need an explicit `@rendermode InteractiveServer` to become interactive; layout components
   (`MainLayout`, `NavMenu`, a custom `UserMenu`) had none, so their `@onclick` handlers were
   completely inert (clicking the avatar did nothing, no error). Fixed by adding
   `@rendermode="InteractiveServer"` to `<Routes />` in `App.razor`, making interactivity
   global — appropriate here since every page requires sign-in anyway. Removed the
   now-redundant per-page directive from `Messages.razor`.
7. **C# records + DataAnnotations + ASP.NET Core have a real footgun**: attributes written
   as `[property: Required, StringLength(35)] string Foo` (targeting the generated property)
   make ASP.NET Core's MVC model binder throw `InvalidOperationException` on every request,
   because it reads validation metadata from a record's constructor *parameters*, not its
   properties, for records. Fix: drop the `[property: ...]` target, apply attributes directly
   to the parameter. Side effect: `System.ComponentModel.DataAnnotations.Validator
   .TryValidateObject` reads from *properties* via `TypeDescriptor` and therefore can no
   longer see parameter-only attributes — it will silently report zero errors. Don't use
   `Validator.TryValidateObject` to test record validation attributes; check via reflection
   on `ConstructorInfo.GetParameters()...GetCustomAttribute<T>()` instead (see
   `CreatePacs008MessageRequestTests`).
8. Key Vault secret naming convention settled: **`{resource}-{purpose}`**, e.g.
   `sql-admin-password`, `web-client-secret` — flat, domain-readable, kebab-case. Deliberately
   not using the `--`→`:` double-hyphen auto-mapping some libraries expect, since that would
   force awkward names just to satisfy .NET config-section nesting; instead each consumer
   maps explicit secret names to config keys in code (see `AzureSuiteKeyVaultSecretManager`
   in `AzureSuite.Web/Program.cs`).

## Domain pivot: User/auth → PACS.008 messages (2026-09-02)

Original plan had the API doing its own register/login/password-hashing/JWT-issuing. On
reflection (mid-build, after the User entity + migration were already applied to Azure SQL):
if the goal is **SSO**, Entra ID should be the *only* identity source — an app-owned user
table with passwords defeats the point of SSO and duplicates what Entra ID already does.
So: the API will validate Entra ID-issued tokens (resource server), not manage users itself.

Separately, decided the business domain itself should be more realistic/interesting than
generic CRUD entities, to give Service Bus/Functions/Mongo a genuine reason to exist. Landed
on a simplified **ISO 20022 pacs.008** (FIToFICustomerCreditTransfer) payment message:
`Pacs008Message` (MessageId, EndToEndId, Amount, Currency, RemittanceInformation, Status,
timestamps) with `Debtor`/`Creditor` as an EF Core **owned type** (`PartyAccount`: Name,
Iban, BicCode) — flattened into columns on the same table (`DebtorName`, `DebtorIban`, etc.),
not a separate table, since a party has no identity/lifecycle independent of its message.

Mechanically: rolled back the old `Users` migration (`dotnet ef database update 0`), removed
it (`dotnet ef migrations remove`), replaced the entity + `AppDbContext` config, wrote new
tests first (`Pacs008MessageTests`, updated `AppDbContextTests`), then generated and applied
a fresh `InitialCreate` migration against the same live Azure SQL database.

## EF Core + SQL (done; entity superseded, tooling/setup below still accurate)

Originally built against a `User` entity; superseded by the domain pivot above
(`Pacs008Message`) but the EF Core plumbing/tooling notes below are unchanged.

- `AzureSuite.Domain/Entities/Pacs008Message.cs` — see domain pivot section above.
- `AzureSuite.Infrastructure/Persistence/AppDbContext.cs` — `DbSet<Pacs008Message>`,
  configures the unique index on `MessageId` + owned-type mapping for Debtor/Creditor.
- EF Core packages: `Microsoft.EntityFrameworkCore.SqlServer` + `.Design` on Infrastructure
  (Design is `PrivateAssets=all`, tooling-only) — **and also on `AzureSuite.Api`**, because
  `dotnet ef` needs Design directly on the *startup* project, it doesn't flow transitively.
- Connection string lives in **.NET User Secrets** on `AzureSuite.Api` (key
  `ConnectionStrings:AzureSuiteDb`), never in `appsettings.json` or git. Built by reading
  the password straight out of Key Vault into a PowerShell variable and setting the secret
  in one step — the password itself never appeared in any transcript/log.
  To recreate on a new machine:
  ```powershell
  $sqlPassword = az keyvault secret show --vault-name kv-azsuite-dev-pumpkin --name sql-admin-password --query "value" -o tsv
  $connString = "Server=tcp:sql-azuresuite-dev-pumpkin.database.windows.net,1433;Database=azuresuite;User ID=sqladmin;Password=$sqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  cd src/AzureSuite.Api
  dotnet user-secrets set "ConnectionStrings:AzureSuiteDb" "$connString"
  ```
- `dotnet-ef` installed as a global tool (`dotnet tool install --global dotnet-ef`).
- Local machine's public IP was allow-listed on the SQL server firewall (separate from the
  Bicep-managed `AllowAzureServices` rule, since home IPs change):
  ```powershell
  az sql server firewall-rule create --resource-group rg-azuresuite-dev --server sql-azuresuite-dev-pumpkin --name AllowMyDevMachine --start-ip-address <ip> --end-ip-address <ip>
  ```
  If SQL connections start failing with a network-ish error, re-run this with your current IP.
- `Program.cs` registers `AppDbContext` via `AddDbContext` with `EnableRetryOnFailure()` —
  required because the database is **serverless** and auto-pauses after 60 min idle; the
  first connection after a pause throws a transient SQL error (40613) while it resumes
  (~30-60s), which the retry policy absorbs.
- Migration `InitialCreate` created and applied directly against the real Azure SQL
  database (not a local one) — this project intentionally develops against the live
  free-tier resource rather than a local SQL container, to stay close to what "testing
  against real Azure" looks like.

## Session recovery checklist

If starting fresh: `git log --oneline` to see what's actually committed, `az account show`
to confirm login/subscription is still active, `az group show -n rg-azuresuite-dev` to
confirm the resource group is reachable, then re-read the "Open items" section above.
