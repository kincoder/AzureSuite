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
| `Ingestion.Api` | `POST /messages`, Scalar at `/scalar/v1`, health at `/health` |

## API

| Method | Route | Body | Returns |
|---|---|---|---|
| `POST` | `/messages` | `{ messageType, version, payload }` | `{ messageId, messageType, version }` |

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
- The app runs as `Development` on Azure so Scalar is reachable. As a side effect, unhandled
  errors return the developer exception page.
