# AzureSuite — Progress Log

This is a study-case C# solution to hands-on learn most of Azure: API auth, SSO UI,
SQL + Mongo, Service Bus, Functions, App Insights, and IaC (Bicep) + CI/CD (GitHub Actions).

Read this file at the start of a new session to recover context without replaying the
whole conversation. Update it after each meaningful step (decision made, resource created,
file added) — keep entries short and factual, not a transcript.

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
  AzureSuite.Api/            → Web API (register/login, JWT) — not yet implemented
  AzureSuite.Web/            → Blazor Server UI (SSO) — not yet implemented
  AzureSuite.Functions/      → isolated-worker Azure Functions — not yet implemented
  AzureSuite.Domain/         → entities — empty scaffold (Class1.cs) so far
  AzureSuite.Application/    → use cases/interfaces — empty scaffold so far
  AzureSuite.Infrastructure/ → EF Core, Mongo, Service Bus clients — empty scaffold so far
tests/
  AzureSuite.Tests/
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

1. ~~EF Core + SQL~~ — done, see below.
2. Auth: ASP.NET Identity + JWT issuing in the API (register/login).
3. Mongo logging: start with local container, later Cosmos DB (Mongo API, free tier).
4. Blazor Web: SSO via Entra ID app registration.
5. Service Bus + Functions wiring.
6. Application Insights / Log Analytics.
7. GitHub Actions pipeline (deploy infra + apps).

## EF Core + SQL (done)

- `AzureSuite.Domain/Entities/User.cs` — minimal entity: `Id`, `Email` (unique index),
  `PasswordHash`, `CreatedAt`.
- `AzureSuite.Infrastructure/Persistence/AppDbContext.cs` — `DbSet<User>`, configures the
  unique index + max lengths in `OnModelCreating`.
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
