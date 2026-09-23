# Routing & Processing — Design

## Why

Third step in the messaging hub build order (per
`2026-09-11-financial-messaging-hub-design.md`), and a deliberate departure
from what that master spec originally proposed. The master spec described
**Routing** as its own microservice (`Routing.Functions`) that asks Catalog
for routing rules and republishes per subscriber, with **Validation**
folded implicitly into "Ingestion validates against Catalog" (later
corrected by `2026-09-14-ingestion-design.md` to say Ingestion does *not*
validate, deferring that to a future downstream stage).

Working through the shape of that downstream stage surfaced a real
bounded-context question: should routing rules, schema validation, and the
message-processing engine be three separate services, two, or one? A full
microservices split (Catalog / Routing / Validation / Processing, each
independently deployable) was designed and rejected in favor of the
simpler shape below — **not because the split was wrong in principle**
(rate-of-change reasoning: routing rules change far more often than a
schema registry does), but because this is a study project whose immediate
goal is a working, learnable end-to-end path through Cosmos DB, Azure
Functions, and (later) Logic Apps. The finer-grained split remains a valid
future refactor once there's a real reason (independent scaling, a second
team, a second consumer of routing rules) to pay for it — see "Deferred"
below.

## Scope of this increment

In scope: `Catalog.Api` enhancements (`Client`, `Route` entities, their
CRUD endpoints, a schema-validation endpoint, a route-lookup endpoint),
`AzureSuite.Web.Blazor` UI for managing Clients and Routes, a new
`Processing.Functions` service, Cosmos DB for lifecycle events, three new
Service Bus output queues, a small `Ingestion.Api` contract change (adding
`ClientId`, needed by Processing's route lookup), and the CI/CD + infra to
deploy all of it.

Out of scope (deferred, see below): splitting Routing/Validation out of
Catalog into their own services; Delivery; Archive; Monitoring; a Logic
App consumer standing in for a subscriber (proposed during design, not
finalized — revisit once there's an actual downstream consumer story to
build); idempotency dedup; semantic (business-rule) validation beyond
schema shape.

## Catalog enhancements

Catalog remains what it has always been — a static registry that never
changes on its own — but it gains two new entities alongside `MessageType`.
Both are still just data Catalog holds and serves; the endpoints below are
kept deliberately single-purpose rather than one do-everything call.

### New entities

```
Client
  Id: Guid
  Name: string

Route
  Id: Guid
  ClientId: Guid
  MessageTypeName: string
  MessageTypeVersion: string
  QueueNames: IReadOnlyList<string>   // one or more, not exactly one
```

A `Route` is keyed by `(ClientId, MessageTypeName, MessageTypeVersion)` and
resolves to one or more output queue names — a single message type from a
single client can legitimately need to reach more than one downstream
queue (e.g. two different subscribers both want `pacs.008` from the same
sender). No `Route` for a given `(Client, MessageType)` pair means that
client is not authorized to send that message type.

### New endpoints

```
POST   /clients                                  → create
GET    /clients                                  → list
GET    /clients/{id}                              → get
PUT    /clients/{id}                              → update
DELETE /clients/{id}                              → delete

POST   /routes                                    → create
GET    /routes                                    → list
GET    /routes/{id}                                → get
PUT    /routes/{id}                                → update
DELETE /routes/{id}                                → delete

POST   /message-types/{name}/{version}/validate   → { payload: string }
                                                     → { isValid: bool, errors: string[] }

GET    /routes/lookup?clientId={id}&messageType={name}&version={v}
                                                    → { queueNames: string[] }   // [] = not authorized
```

Two endpoints are deliberately kept separate rather than combined:

- **`validate`** checks only whether a payload is well-formed for a
  registered message type. It takes no client and knows nothing about
  routing.
- **`routes/lookup`** checks only whether a client is authorized to send a
  message type, and if so, where it goes. It takes no payload and does no
  schema work.

Processing (below) calls both, but they answer different questions and
neither depends on the other's result to be computed — keeping them apart
means either can be called independently (e.g. a future caller that only
wants to check authorization, or only wants to check payload shape,
without paying for or triggering the other).

`validate` runs the existing `MessageType.SchemaDefinition` (already
present in Catalog, currently unused by anything — see
`2026-09-14-ingestion-design.md`'s correction that Ingestion never actually
validates) through a JSON Schema validator (`JsonSchema.Net`) against the
submitted payload.

### CQRS handlers

Same MediatR pattern as `MessageType`: `CreateClientCommand`,
`UpdateClientCommand`, `DeleteClientCommand`, `GetClientQuery`,
`ListClientsQuery`; `CreateRouteCommand` / `UpdateRouteCommand` /
`DeleteRouteCommand` / `GetRouteQuery` / `ListRoutesQuery` /
`LookupRouteQuery`; `ValidateMessageQuery`. Each in its own
`Catalog.Application/{Clients,Routes,MessageTypes}/{Commands,Queries}/<Name>/`
folder, matching `RegisterMessageType`'s existing layout.

### Persistence

`Client` and `Route` join `MessageType` in `CatalogDbContext` (EF Core, SQL
Server) — one new migration. `Route.QueueNames` stored as a simple
delimited column or an owned collection table (`RouteQueue(RouteId,
QueueName)`); the latter is the EF-idiomatic choice for a
one-to-many list and is what this increment uses.

## `Processing.Functions`

New service, `services/Processing/`. Owns no data of its own — every piece
of state it touches belongs to Catalog (Client/Route/MessageType) or
Cosmos (the lifecycle event it writes). Its job is purely orchestration.

### Trigger and pipeline

Service Bus–triggered on `messages.raw` (the existing Ingestion output
queue). Per message:

1. Pick up the message (`messageId`, `messageType`, `version`, `payload`,
   plus `clientId` — see "Client identification" below).
2. Call Catalog's `validate` endpoint with `(messageType, version,
   payload)`.
   - Invalid → write a `Rejected` lifecycle event (reason: validation
     errors) → dead-letter the Service Bus message → done.
3. Call Catalog's `routes/lookup` endpoint with `(clientId, messageType,
   version)`.
   - Empty result → write a `Rejected` lifecycle event (reason: client not
     authorized for this message type) → dead-letter → done.
4. **Fan-out**: for each queue name in the result, publish a copy of the
   message to that queue via the Service Bus SDK's plain queue send. This
   is done explicitly in code — deliberately not using Service Bus
   topics/subscriptions (Basic tier doesn't support them, and this project
   is standardizing on explicit multi-queue publish as the permanent
   approach, not a tier-driven workaround).
5. Write a `Routed` lifecycle event (queue names it fanned out to).
6. Complete the Service Bus message.

Any unhandled exception (e.g. Catalog temporarily unreachable) leaves the
message uncompleted — Service Bus's own lock/redelivery handles the retry,
rather than Processing inventing its own retry mechanism.

### Client identification

Ingestion's request contract today has no `clientId` field —
`IngestMessageRequest` is `{ messageType, version, payload }` only. This
increment adds `ClientId` to that contract (`Ingestion.Api`'s request DTO
and the Service Bus message's application properties, alongside the
existing `messageType`/`version`), since Processing cannot look up a route
without knowing who sent the message. This is a small, mechanical change
to `Ingestion.Api`/`IngestMessageCommand`, not a redesign of Ingestion.

### Lifecycle events (Cosmos DB)

```
{
  id: <messageId>,
  messageType, version, clientId,
  status: "Rejected" | "Routed",
  reason: string?,          // set on Rejected
  queueNames: string[]?,    // set on Routed
  timestampUtc: DateTime
}
```

One container, partitioned by `messageId`. Nothing reads this yet — it's
built now specifically as the hands-on Cosmos exercise, ahead of
Monitoring (which will be its first real consumer once it exists).

### Project layout

```
services/Processing/
  Processing.Application/
    Abstractions/ICatalogClient.cs, ILifecycleEventStore.cs, IFanOutPublisher.cs
    Messages/Commands/ProcessInboundMessage/
      ProcessInboundMessageCommand.cs
      ProcessInboundMessageCommandHandler.cs
  Processing.Infrastructure/
    Catalog/CatalogHttpClient.cs
    Messaging/ServiceBusFanOutPublisher.cs
    Persistence/CosmosLifecycleEventStore.cs
  Processing.Functions/
    ProcessInboundMessageFunction.cs   // Service Bus trigger, thin — delegates to the command
tests/
  Processing.Application.Tests/
  Processing.Infrastructure.Tests/
```

No `Processing.Domain` — same reasoning as Ingestion's original decision:
no entity or invariant of its own to model, just orchestration over other
services' data.

## UI

New pages under `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/`
(alongside the existing `MessageTypesList`/`RegisterMessageType`):
`Clients.razor` (list/create/edit/delete) and `Routes.razor`
(list/create/edit/delete, with a multi-select for queue names). Same
`CatalogApiClient` extended with the new endpoints, same component/testing
patterns as the existing Catalog feature area.

## Test data

Seeded directly via SQL against `CatalogDbContext` (a seed script, not
through the new CRUD API/UI — the API/UI are the real product surface,
seeding is just to have something to run against locally):

- 2 `Client` rows.
- 3 output `Route` queues total.
- Client A: 1 `Route` (single message type → 1 queue).
- Client B: 3 `Route`s.

Exact message-type/queue assignments are decided at seed-script-writing
time, not fixed by this spec.

## Infra

- `infra/modules/catalog/sql.bicep` — extend with the new `Client`/`Route`
  tables (via EF Core migration, not hand-written Bicep DDL — matches how
  `MessageType`'s table already got there).
- `infra/modules/processing/` (new) — `appservice.bicep` → actually a
  **Function App** hosting plan (Consumption or Flex Consumption, not App
  Service — Processing is event-triggered, matching the "Functions for
  event/queue triggers, App Service for synchronous APIs" split already
  established for the other services), plus role assignments for Service
  Bus (receive on `messages.raw`, send on the three output queues) and
  Cosmos (write).
- `infra/modules/shared/servicebus.bicep` (or extend Ingestion's) — three
  new output queues, Basic tier, same namespace as `messages.raw`.
- `infra/modules/shared/cosmos.bicep` (new) — Cosmos DB account (Serverless
  capacity mode — cheapest option for low, bursty write volume, matching
  this project's free/low-cost bias elsewhere), one database, one
  container (`LifecycleEvents`, partition key `/messageId`).
- `main.bicep` wires all of the above in.

## CI/CD

`deploy-catalog-api` picks up the new endpoints automatically (same
deployable, no new job). New `deploy-processing-functions` job in
`build.yml`, mirroring the existing API deploy jobs' shape but targeting a
Function App instead of an App Service (`Azure/functions-action` instead
of `azure/webapps-deploy`). `deploy-infra.yml`'s existing `infra/**`
trigger picks up the new Bicep modules with no workflow change.

## Testing

- `CreateClientCommandHandlerTests`, `CreateRouteCommandHandlerTests`,
  etc. — fake repositories, same pattern as `RegisterMessageTypeHandler`'s
  existing tests.
- `ValidateMessageQueryHandlerTests` — real `JsonSchema.Net` validation
  against known-good/known-bad payloads, no fakes needed (pure function
  once the schema string is in hand).
- `LookupRouteQueryHandlerTests` — covers the "no route → empty list"
  case explicitly, since that's the authorization-denied path.
- `ProcessInboundMessageCommandHandlerTests` — fake `ICatalogClient`,
  `IFanOutPublisher`, `ILifecycleEventStore`; covers all three outcomes
  (rejected-invalid, rejected-unauthorized, routed-with-fanout) and the
  multi-queue fan-out case specifically (N > 1 queues).
- Blazor component tests for `Clients.razor`/`Routes.razor`, same bUnit
  patterns as the existing Catalog pages.
- Warning-free, 1:1 test project per source project, per existing
  conventions.

## Deferred

- **Splitting Routing/Validation/Processing into independent services.**
  The rate-of-change argument for separating them remains valid — if
  `Route` data starts changing much faster than `MessageType` data, or a
  second consumer needs routing rules independent of Catalog, revisit.
  Not worth the extra deployables until one of those becomes true.
- **Logic App as an output-queue consumer**, standing in for a subscriber.
  Proposed during design as the natural fit for hands-on Logic Apps
  experience (a low-code queue-drain, no custom logic needed) but not
  finalized — build when there's an actual "what happens to a routed
  message" story to tell, i.e. alongside or instead of Delivery.
- **Delivery, Archive, Monitoring** — unchanged from the master spec's
  build order, all still after this increment.
- **Idempotency / dedup**, **semantic validation** beyond schema shape —
  same reasoning as `2026-09-14-ingestion-design.md`: deferred until
  there's a concrete case that needs them.
