# Ingestion

**Where:** [services/Ingestion/](../../services/Ingestion/) · **Runs on:** App Service
`app-messaginghub-ingestion-dev` · **Data:** none (stateless) · **Publishes to:** Service Bus
queue `messages.raw`

## What it solves

Ingestion is the hub's front door. Producers need one stable endpoint to hand messages to. That
endpoint must accept a message quickly and durably, without waiting for downstream processing.
Ingestion stamps a `MessageId` on each message, puts it on Service Bus and returns. Everything
else happens asynchronously.

On purpose, it **doesn't validate** the payload against Catalog. Validation belongs to the
downstream processing stage, which keeps the front door fast and decoupled. See
[2026-09-14-ingestion-design.md](../superpowers/specs/2026-09-14-ingestion-design.md).

## Layers

| Project | Responsibility |
|---|---|
| `Ingestion.Application` | `IngestMessageCommand` and its handler: creates a v7 GUID `MessageId` and publishes through `IMessagePublisher` |
| `Ingestion.Infrastructure` | `ServiceBusMessagePublisher`: sends the payload as the body, with `messageType` and `version` as application properties |
| `Ingestion.Api` | `POST /messages`, Scalar at `/scalar/v1` (local Development only), health at `/health` |

## API

| Method | Route | Body | Returns | Auth |
|---|---|---|---|---|
| `POST` | `/messages` | `{ messageType, version, payload }` | `{ messageId, messageType, version }` | `SubmitMessages` (role `Messages.Submit`) |
| `GET` | `/health` | — | Liveness, runs no checks | anonymous |
| `GET` | `/health/verbose` | — | Each check's detail | any signed-in caller |

### Auth

Callers need an Entra ID access token for `api://<tenantId>/messaginghub-api` carrying the
`Messages.Submit` role. Operators get it by signing in to the web app. Producers use client
credentials; the role is assigned to their app registration (for testing,
`MessagingHub-TestProducer`, defined in [infra/identity/main.bicep](../../infra/identity/main.bicep)).
No token → 401, token without the role → 403.

A producer gets a token like this. The test producer's secret lives in Key Vault
(`kv-msghub-catalog-dev`, secret `testproducer-client-secret`) and never goes into the repo:

```powershell
$token = (Invoke-RestMethod -Method Post `
  -Uri "https://login.microsoftonline.com/46b33134-1b34-42b4-9848-6b40016d2e22/oauth2/v2.0/token" `
  -Body @{ client_id = "94df3590-91d1-4561-a2bc-15128726b3a4"; client_secret = (az keyvault secret show --vault-name kv-msghub-catalog-dev --name testproducer-client-secret --query value -o tsv); grant_type = "client_credentials";
           scope = "api://46b33134-1b34-42b4-9848-6b40016d2e22/messaginghub-api/.default" }).access_token

Invoke-RestMethod -Method Post -Uri "https://app-messaginghub-ingestion-dev.azurewebsites.net/messages" `
  -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" `
  -Body '{"messageType":"pacs.008","version":"1.0","payload":"{}"}'
```

## Running locally

- Config: `ServiceBus:FullyQualifiedNamespace` and `ServiceBus:QueueName` (`messages.raw`).
- Auth to Service Bus uses your `az login` identity (`AzureCliCredential` locally). That
  identity needs **Azure Service Bus Data Sender** on the queue.
- Runs at `https://localhost:7185`.

## Deployment

The `deploy-ingestion-api` job in [build.yml](../../.github/workflows/build.yml) does the
deploy. On Azure the app uses its system-assigned managed identity, detected through the
`IDENTITY_ENDPOINT` variable. The Bicep grants that identity Data Sender on the queue.

## Known gaps

- `ClientId` isn't in the contract yet. Processing's route lookup will need it.
