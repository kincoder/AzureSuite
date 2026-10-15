# Infrastructure & CI/CD

All resources live in resource group **`rg-messaginghub-dev`** (West Europe), subscription
Pay-As-You-Go. Names follow `<type>-messaginghub-<service>-dev`. Key Vault is shortened to
`kv-msghub-…` because of its 24-character limit.

## Bicep

[infra/main.bicep](../infra/main.bicep) wires the modules together, and
[infra/main.dev.bicepparam](../infra/main.dev.bicepparam) holds the dev values. The SQL
password is pulled from Key Vault with `az.getSecret()` and never stored in the repo.

| Module | Creates |
|---|---|
| `shared/appinsights.bicep` | Log Analytics + Application Insights (shared by all services) |
| `catalog/sql.bicep` | SQL server + serverless `catalog` database (free tier) |
| `catalog/keyvault.bicep` | `kv-msghub-catalog-dev`, connection string secret, RBAC |
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
There's no client secret. Environment-specific config (API URLs, the SWA token, the App
Insights connection string) lives in the `azure-dev` GitHub environment.

### CI principal permissions

- Contributor on the resource group.
- Role Based Access Control Administrator on the resource group, **with a condition** that
  allows only the roles the Bicep assigns: Key Vault Secrets User/Officer, Service Bus Data
  Sender/Receiver, and Storage Blob Data Owner. If a new module assigns any other role, add
  that role's id to the condition first, or Deploy Infra fails.
- Key Vault Secrets User on `kv-msghub-catalog-dev`, so `az.getSecret` can resolve.
