# Ingestion — Design

## Why

Second service in the messaging hub build order (per
`2026-09-11-financial-messaging-hub-design.md`), depending only on Catalog
being live, which it is. This spec refines and, in a few places, corrects
the master design's original description of Ingestion — several
architectural questions surfaced while designing this service that the
master spec hadn't resolved, and the answers change what Ingestion actually
does.

## Corrections to the master design

The master spec said Ingestion "validates against Catalog (sync call)" and,
separately, that Routing "dead-letters malformed messages." Working through
what "validation" actually means surfaced four distinct concerns the master
spec had conflated into one:

1. **Identification** — what type does this message claim to be?
2. **Structural validation** — is the payload well-formed against the
   registered schema?
3. **Semantic validation** — business rules the schema can't express
   (e.g. settlement date not in the past).
4. **Routing** — given a valid message, who should receive it?

"Invalid message" and "valid message, no route" are the same *kind* of
problem — both are "this message can't proceed as submitted," both need the
same handling (dead-letter, lifecycle event), both are naturally discovered
after the message is already in the pipeline. Treating one as a synchronous
gate-reject and the other as an async dead-letter was an inconsistency, not
a deliberate design.

**Resolution**: Ingestion validates only its own contract (the request
envelope's shape — are `messageType`/`version`/`payload` present and
non-empty). It makes **no synchronous call to Catalog or anywhere else**.
Identification, structural validation, semantic validation, and routability
are unified into a single downstream concern — a future async Validation
stage (its own subscription off the raw queue/topic, alongside Routing) that
dead-letters on any failure, the same way "unroutable" already would. This
future stage is **not built in this increment** — this spec only covers
Ingestion itself, and Ingestion's design must not preclude it.

Two more decisions, made and deliberately deferred, worth recording:

- **No Outbox pattern for Ingestion.** Outbox solves the dual-write problem
  (local DB write + broker publish must be atomic). Ingestion has no local
  DB write — it does one thing, publish — so there's no dual-write to
  protect. Reliability instead comes from not acknowledging the producer's
  HTTP request until the Service Bus send has confirmed. (Outbox likely
  *does* apply later to Routing, which writes to Cosmos *and* republishes —
  a real dual-write. Not this spec's concern.)
- **Idempotency-key deduplication is deferred entirely**, not built in this
  increment. Checking for duplicates only makes sense once we know a message
  is good — checking a message we're about to reject anyway is wasted work.
  Revisit once/after the future Validation stage exists.

## Scope of this increment

In scope: `Ingestion.Api`, `Ingestion.Web` (a minimal test-submission form,
not the diagnostics dashboard the master spec originally described for it —
there's no downstream lifecycle data yet to back a real dashboard), Service
Bus infra (Basic tier, a queue not a topic), CI/CD mirroring Catalog's
pattern.

Out of scope: the async Validation stage, idempotency dedup, Routing,
Archive, Ingestion.Web's eventual diagnostics view.

## `Ingestion.Api`

### Endpoint

```
POST /messages
{ "messageType": "pacs.008", "version": "1.0", "payload": "..." }
```

Request DTO validation only (all three fields required/non-empty) — 400 on
failure, no external calls made to produce that 400.

### Pipeline

MediatR command, matching Catalog's CQRS layering:

1. `IngestMessageCommand(MessageType, Version, Payload)` handled by
   `IngestMessageCommandHandler`.
2. Mint `MessageId = Guid.CreateVersion7()` — native .NET 9+ UUIDv7
   (RFC 9562), a standard `Guid`, time-ordered/sortable, no new package.
   Chosen over a real ULID library specifically to avoid a type that needs
   conversion at every boundary (Service Bus `MessageId`, future Cosmos
   document IDs, EF Core keys all already understand `Guid` natively).
3. Send via `IMessagePublisher` (Application abstraction) →
   `ServiceBusMessagePublisher` (Infrastructure implementation) — same
   Abstractions/Infrastructure split as Catalog's `IMessageTypeRepository`.
   The Service Bus message's native `MessageId` property carries the minted
   ID (sets up, for later, Service Bus's own duplicate-detection feature —
   Standard-tier only, not enabled this increment, but the property is the
   right one to use regardless so nothing needs to change later).
4. Await the send before returning — the HTTP response is only ever "201"
   once Service Bus has durably accepted the message.
5. Response: `201 { messageId, messageType, version }`.

### Auth to Service Bus

Managed identity + RBAC (`Azure Service Bus Data Sender` role scoped to the
queue) — no connection string, matching the pattern already established for
Catalog.Api's Key Vault access.

### Project layout

```
services/Ingestion/
  Ingestion.Application/
    Abstractions/IMessagePublisher.cs
    Messages/Commands/IngestMessage/
      IngestMessageCommand.cs
      IngestMessageCommandHandler.cs
      IngestedMessageDto.cs
  Ingestion.Infrastructure/
    Messaging/ServiceBusMessagePublisher.cs
  Ingestion.Api/
    Contracts/IngestMessageRequest.cs
    Program.cs
tests/
  Ingestion.Application.Tests/
  Ingestion.Infrastructure.Tests/
  Ingestion.Api.Tests/
```

No `Ingestion.Domain` project this increment — there's no entity or value
object to model yet (no persistence, no business rules beyond "are these
three fields present"). Add it when there's an actual domain concept to
carry, rather than scaffolding an empty project now.

## `Ingestion.Web`

One Blazor WASM page: a form (message type, version, payload textarea) →
`POST`s to Ingestion.Api → shows the returned `MessageId` on success or the
error on failure. Reuses `AzureSuite.Web.UI` shared components, same
project shape as `Catalog.Web`. Serves as the interim producer-simulation
tool the master spec called for ("producers are simulated within this repo
initially").

## Infra

New modules under `infra/modules/ingestion/`, mirroring Catalog's:

- `servicebus.bicep` — Service Bus namespace, **Basic tier**, one queue
  `messages.raw`. Basic tier chosen deliberately over Standard to stay
  free/low-cost like every other resource so far — but Basic does not
  support topics, so this is a **queue**, not the topic the master spec's
  end-to-end flow eventually needs for Archive+Routing fan-out. Revisit the
  tier (and queue→topic migration) when Archive/Routing are built and
  fan-out is actually needed; accepting that cost then, not speculatively
  now.
- `appservice.bicep` — Linux App Service, F1, same shape as Catalog.Api's.
- `staticwebapp.bicep` — Free tier, same shape as Catalog.Web's.

`main.bicep` wires these in, grants Ingestion.Api's managed identity the
Service Bus Data Sender role on the queue.

## CI/CD

`deploy-ingestion-api` and `deploy-ingestion-web` jobs in `build.yml`,
mirroring `deploy-catalog-api`/`deploy-catalog-web` exactly: gated on
`build-and-test`, `azure-dev` environment, post-deploy smoke test (health
check for the API, appsettings substitution check for the web app — same
pattern just built for Catalog). New `azure-dev` variable
`INGESTION_API_BASE_URL` for the web form's target and the smoke test.

`deploy-infra.yml`'s existing `infra/**` path trigger picks up the new
bicep modules automatically — no workflow change needed there.

## Testing

- `IngestMessageCommandHandlerTests` — fake `IMessagePublisher`, no real
  Service Bus in unit tests.
- Request DTO validation tests (missing/empty fields → 400).
- `ServiceBusMessagePublisherTests` — to the extent testable without a live
  Service Bus (verify the message is built correctly: `MessageId` property
  set, body/application properties correct); actual send behavior isn't
  unit-testable and isn't covered by an integration test this increment
  (no local Service Bus emulator wired up yet).
- Warning-free, 1:1 test project per source project, per existing
  conventions.
