# Processing

**Where:** [services/Processing/](../../services/Processing/) · **Runs on:** Function App
`func-messaginghub-processing-dev` (.NET 10 isolated worker, Flex Consumption) · **Data:**
Cosmos DB `cosmos-messaginghub-dev` → database `messaginghub`, container `LifecycleEvents`
(partition key `/messageId`)

## What it solves

Once a message is on the queue, something has to pick it up and record what happened to it.
Processing is that engine. Today it **receives and logs** each message, so every message
Ingestion accepts has an audit trail. Later it will also validate the message, look up its
routes and fan it out to the output queues.

## Functions

| Function | Trigger | What it does |
|---|---|---|
| `RecordInboundMessage` | Service Bus, queue `messages.raw` | Writes a `Received` lifecycle event to Cosmos. Settlement is explicit: the message is completed after the write, dead-lettered right away (`MalformedMessage`) if `messageType`/`version` are missing, and left unsettled on errors so Service Bus redelivers it (up to 10 times, then it's dead-lettered). |
| `RunHousekeeping` | Timer `0 0 0 * * *` (midnight **UTC**) | Moves the dead-letter queue into the log as `DeadLettered` events, then purges events older than `Housekeeping__RetentionDays` (30). |

## Layers

| Project | Responsibility |
|---|---|
| `Processing.Application` | `RecordInboundMessageCommand`, `RunHousekeepingCommand`, abstractions `ILifecycleEventStore` and `IDeadLetterQueue` |
| `Processing.Infrastructure` | `CosmosLifecycleEventStore`, `ServiceBusDeadLetterQueue` |
| `Processing.Functions` | Function host, triggers, `InboundMessageReader`, `HousekeepingOptions` |

## Running locally

1. Create `services/Processing/Processing.Functions/local.settings.json` (it's gitignored)
   with `AzureWebJobsStorage=UseDevelopmentStorage=true`,
   `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`, and the same `ServiceBus__*`, `Cosmos__*` and
   `HousekeepingSchedule` values the Bicep sets.
2. Start Azurite: `npx azurite --location .azurite`
3. Run the `run: Processing.Functions` VS Code task (`func start`).

Your `az login` identity needs Service Bus Data Receiver on the queue. Note that a local host
**competes with the deployed app** for messages on `messages.raw`.

## Seeing it work on Azure

Submit a message from the web app. Then open Cosmos → Data Explorer → `messaginghub/LifecycleEvents`,
where a `Received` document appears within seconds. Logs are in App Insights `traces`, where
`cloud_RoleName == "func-messaginghub-processing-dev"`.

## Next steps

Inside `RecordInboundMessageCommandHandler`:

1. Validate the message through Catalog `validate`.
2. Look up routes through Catalog `routes/lookup`. This needs `ClientId` from Ingestion.
3. Publish to each output queue.
4. Write `Validated` / `Routed` / `Rejected` events.

Plan: [2026-09-23-processing-functions.md](../superpowers/plans/2026-09-23-processing-functions.md).
Use Flex Consumption (FC1) rather than the plan's Y1, and `__` rather than `:` in app settings.
