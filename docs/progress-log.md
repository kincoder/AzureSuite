# AzureSuite — Progress Log

This is a study-case C# solution to hands-on learn most of Azure: API auth, SSO UI,
SQL + Mongo, Service Bus, Functions, App Insights, and IaC (Bicep) + CI/CD (GitHub Actions).

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

1. ~~EF Core + SQL~~ — done, see below (superseded once by the domain pivot, both done).
2. **Auth, revised direction**: API is a resource server validating Entra ID-issued JWTs
   (`AddAuthentication().AddJwtBearer(...)` against the tenant) — no app-owned user store,
   no custom register/login/password hashing. Needs: an **App Registration** in Entra ID
   for the API (Portal walkthrough, not yet done), then wiring `Microsoft.Identity.Web` or
   plain JWT bearer config in `Program.cs`, then `[Authorize]` on message endpoints.
3. Domain pivoted from generic `User` CRUD to **PACS.008 payment messages** (see below) —
   a much better fit for the Service Bus/Functions/Mongo pipeline than a CRUD user table.
4. Mongo logging: start with local container, later Cosmos DB (Mongo API, free tier) —
   likely storing the raw message payload/audit trail, complementing the SQL record.
5. Blazor Web: SSO via the same Entra ID app registration (or a separate one for
   interactive users vs. the API's own registration — decide when we get there).
6. Service Bus + Functions wiring: API receives a message → publishes to Service Bus →
   Function consumes → persists to Mongo (audit) + SQL (reference/settlement data).
7. Application Insights / Log Analytics — trace the whole pipeline end to end.
8. GitHub Actions pipeline (deploy infra + apps).

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
