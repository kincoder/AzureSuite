# Infrastructure & CI/CD

All resources live in resource group **`rg-messaginghub-dev`** (West Europe), subscription
Pay-As-You-Go. Names follow `<type>-messaginghub-<service>-dev`. Key Vault is shortened to
`kv-msghub-…` because of its 24-character limit.

## Bicep

[infra/main.bicep](../infra/main.bicep) wires the modules together, and
[infra/main.dev.bicepparam](../infra/main.dev.bicepparam) holds the dev values. There are
no secrets in either: every service signs in with its managed identity.

| Module | Creates |
|---|---|
| `shared/appinsights.bicep` | Log Analytics (0.15 GB daily cap) + Application Insights (shared by all services) |
| `catalog/sql.bicep` | Entra-only SQL server + serverless `catalog` database (free tier) |
| `catalog/keyvault.bicep` | `kv-msghub-catalog-dev` for hand-set secrets, RBAC |
| `catalog/appservice.bicep` | `app-messaginghub-catalog-dev` |
| `ingestion/appservice.bicep` | `app-messaginghub-ingestion-dev` |
| `ingestion/servicebus.bicep` | Service Bus namespace, queue `messages.raw`, Data Sender for Ingestion |
| `shared/cosmos.bicep` | Cosmos account (free tier, keys disabled), `messaginghub/LifecycleEvents` |
| `processing/functionapp.bicep` | Flex Consumption plan, storage, `func-messaginghub-processing-dev` |
| `processing/servicebus-receiver.bicep` | Data Receiver on `messages.raw` for the Function App |
| `shared/cosmos-data-contributor.bicep` | Cosmos data-plane role assignments |
| `web/staticwebapp.bicep` | `stapp-messaginghub-web-dev` |

Role assignments that depend on two resources sit in their own modules, deployed after both
resources. This avoids dependency cycles.

Preview a change before merging:

```powershell
az deployment group what-if -g rg-messaginghub-dev -f infra/main.bicep -p infra/main.dev.bicepparam
```

## GitHub Actions

| Workflow | Trigger | Jobs |
|---|---|---|
| [build.yml](../.github/workflows/build.yml) | Push / PR | `build-and-test` always runs. On `master`: `deploy-web`, `deploy-catalog-api` (with EF migrations), `deploy-ingestion-api`, `deploy-processing-functions` |
| [deploy-infra.yml](../.github/workflows/deploy-infra.yml) | Push to `master` touching `infra/**`, or manual | `az deployment group create` |

Azure auth uses **OIDC federation** through the app registration `gh-actions-azuresuite`.
There's no client secret, and no publish profile: App Service deploys use the same OIDC
session, and basic-auth publishing is off on both apps. Config (API URLs, the SWA token, the
App Insights connection string) lives in **repository** secrets and variables.

The repo is private on GitHub Free, which has no environments or branch protection for
private repos. "Only `master` deploys" is enforced on the Azure side instead: the federated
credential's subject is
`repo:kincoder@18114781/AzureSuite@1353255818:ref:refs/heads/master`, so a workflow run on any
other branch can't sign in.

### CI principal permissions

- Contributor on the resource group.
- Role Based Access Control Administrator on the resource group, **with a condition** that
  allows only the roles the Bicep assigns: Key Vault Secrets User/Officer, Service Bus Data
  Sender/Receiver, and Storage Blob Data Owner. If a new module assigns any other role, add
  that role's id to the condition first, or Deploy Infra fails.
- A contained user in the `catalog` database (`db_ddladmin`, `db_datareader`,
  `db_datawriter`) so `deploy-catalog-api` can run EF migrations. See below.

### SQL: Entra-only

The server accepts Microsoft Entra sign-ins only; the `sqladmin` login it was created with
can no longer connect. The owner is the Entra admin. The API and CI are contained database
users, created once by the owner with `CREATE USER [name] WITH SID = <client id as binary>,
TYPE = E` (the SID form needs no Graph lookup by the server):

| User | Who | Roles |
|---|---|---|
| `app-messaginghub-catalog-dev` | The Catalog API's managed identity (client id `06795f07-…`) | `db_datareader`, `db_datawriter` |
| `gh-actions-azuresuite` | The CI principal (client id `1f58adc7-…`) | `db_ddladmin`, `db_datareader`, `db_datawriter` |

If either identity is recreated, its client id changes and the user must be recreated.

## Cost and exposure guardrails

Bots find any public endpoint. These settings bound what that traffic can cost:

| Resource | Bound |
|---|---|
| App Services (F1), Static Web App (Free) | Free tiers: past the quota they stop serving, never bill |
| SQL | Free offer with `freeLimitExhaustionBehavior: AutoPause`. Anonymous `/health` runs no checks, so probes don't keep it awake |
| Cosmos | Free tier; the database's 1000 RU/s is exactly the free amount, so it throttles before it bills |
| Function App (Flex) | `maximumInstanceCount: 1`. Only Service Bus messages trigger it |
| Service Bus | `disableLocalAuth`: no SAS keys, only the APIs' managed identities can send |
| Log Analytics | 0.15 GB daily cap, inside the 5 GB monthly free allowance |
| Subscription | Budget `budget-subscription-monthly` (€5) emails the owner at 20%, 100% and forecast 100% |

The APIs run as `Production` on Azure, so there is no developer exception page and no
anonymous OpenAPI/Scalar (both stay available locally). `/health/verbose` needs a token.

### Parking the apps between work sessions

```powershell
./scripts/azure-dev.ps1 stop     # stop both App Services and the Function App
./scripts/azure-dev.ps1 start    # start them, wait for /health
./scripts/azure-dev.ps1 status
```

A stopped app answers 403 to everything. CI still deploys to it but skips the health check.

## Identity (Entra ID): deployed manually

[infra/identity/main.bicep](../infra/identity/main.bicep) uses the **Microsoft Graph Bicep
extension** to define the app registrations and roles that secure the APIs. It isn't referenced
by `main.bicep`, and `deploy-infra.yml` ignores `infra/identity/**`. The owner deploys it with
their own `az login`, so the CI principal needs no Microsoft Graph permissions.

```powershell
az deployment group create -g rg-messaginghub-dev `
  -f infra/identity/main.bicep -p infra/identity/identity.dev.bicepparam `
  --query properties.outputs
```

| Creates | Purpose |
|---|---|
| `MessagingHub-Api` | One API registration for Catalog and Ingestion: app roles `Catalog.Reader`, `Catalog.Admin`, `Messages.Submit`; scope `access_as_user`; identifier URI `api://<tenantId>/messaginghub-api` |
| `MessagingHub-Web` | The Blazor SPA (public client), redirect URIs for the Static Web App and localhost |
| `MessagingHub-TestProducer` | A stand-in producer that holds `Messages.Submit` |
| Consent grants | Web → API `access_as_user` and Graph `openid profile offline_access`, tenant-wide, so there's no consent prompt |
| Role assignments | The owner gets every role; the test producer gets `Messages.Submit` |

**Limitations:**
- Graph Bicep can't create client secrets. Create the producer's secret once with
  `az ad app credential reset` and store it straight in Key Vault as
  `testproducer-client-secret` (in `kv-msghub-catalog-dev`), so it never appears on screen.
- There's no `what-if` for Graph resources.
- A role assignment right after its service principal is created can fail on replication
  delay. Re-running the deployment fixes it.
- Edits made in the Portal aren't detected. Re-running the file restores the declared state.

**Adding a role:**
1. Add a new fixed GUID to `roleIds` and an `appRoles` entry in `main.bicep`.
2. Add a constant to `AppRoles` and a policy to `AddAzureSuitePolicies` in
   `services/Shared/AzureSuite.Security`.
3. Use the policy on the endpoints and pages that need it, then redeploy identity.
