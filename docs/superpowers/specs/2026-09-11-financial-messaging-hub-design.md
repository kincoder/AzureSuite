# Financial Messaging Hub — Design

## Why this reset

The previous build (PACS.008 payment demo: API + Blazor + SQL + SSO) worked end to
end, but was assembled feature-by-feature without an upfront design. Every new
capability (micro-frontends, more Azure services) felt bolted on rather than
justified by the domain. This spec defines the system end to end *before* any
implementation resumes, so every component exists because the domain needs it.

## Domain

A **financial messaging hub** — a simplified analogue of the kind of message
switch infrastructure Euroclear/SWIFT-style systems run: multiple producers
submit financial messages (ISO 20022, e.g. `pacs.008`, `camt.054`), the hub
validates them against a catalog of registered message types, routes each
message to the subscribers who care about it, delivers it, and keeps a full
audit trail of the whole lifecycle. At least two message types are supported
from the start specifically to force shared logic (catalog, routing, delivery)
to be designed generically rather than special-cased around one type.

Producers and consumers are simulated within this repo initially (a
`producers/` folder, starting with one small test producer) but are designed
to be swappable for genuinely separate external apps later without changing
the hub's contracts.

## Architecture: full microservices + micro-frontends

Each bounded context below is an independently deployable backend service
(own Azure resources, own CI/CD trigger path) and, where relevant, an
independently deployable frontend module. All services and frontends live in
**one repo, one `.slnx`**, organized by folder — a single solution makes
cross-cutting navigation and refactors practical; independent deployability is
a property of project/CI boundaries, not of the solution file.

### Bounded contexts / services

| Service | Responsibility | Owns |
|---|---|---|
| **Catalog** | Registry of message types (schemas, versions), routing rules, subscriber registrations | SQL |
| **Ingestion** | Public API producers submit messages to; validates against Catalog (sync call), stamps a MessageId, publishes to Service Bus | stateless |
| **Routing** | Subscribes to the raw inbound topic; asks Catalog for routing rules; republishes per-subscriber; dead-letters malformed messages | Cosmos (lifecycle events) |
| **Delivery** | Drains per-subscriber queues; attempts delivery (webhook/pull); retries with dead-letter after N failures | SQL (delivery attempts/status) |
| **Archive** | Subscribes to the raw inbound topic (separate subscription from Routing); writes immutable original payload to Blob | Blob Storage |
| **Monitoring** | Read-only aggregator: queries Cosmos (lifecycle) + Delivery + Catalog *through their own APIs*, never their tables directly; serves the ops dashboard | none |

No service reads another service's database directly — cross-service reads go
through that service's API, matching the deployability boundary.

### End-to-end data flow

1. Producer → `POST` to **Ingestion** with a raw message + declared type.
2. Ingestion calls **Catalog** (sync HTTP) to validate the type is registered
   and the payload matches the registered schema version; 400 if not.
3. Ingestion stamps a `MessageId`, publishes to Service Bus topic
   `messages.raw` (**single topic, filtered subscriptions** — not one topic
   per message type — so adding a message type never requires new topic
   provisioning).
4. Two subscriptions off `messages.raw`: **Routing** and **Archive**, each
   getting their own copy (fan-out).
5. **Archive** writes the raw payload to Blob keyed by `MessageId`.
6. **Routing** asks Catalog for routing rules for this message type, publishes
   one message per interested subscriber to per-subscriber queues, and writes
   lifecycle events (`Received`, `Routed`) to Cosmos.
7. **Delivery** drains each subscriber queue, attempts delivery, records the
   attempt + outcome in SQL, writes `Delivered`/`Failed` lifecycle events to
   Cosmos; failures dead-letter after N retries.
8. **Monitoring** aggregates lifecycle timeline + delivery stats + catalog
   data for the dashboard.

### Frontends (micro-frontends, one per bounded context)

| Frontend | Calls | Purpose |
|---|---|---|
| **Shell** | all MFEs | Composition/nav/shared chrome only, no business logic |
| **Catalog.Web** | Catalog API | Register/edit message types, schema versions, subscriber registrations, routing rules |
| **Ingestion.Web** | Ingestion API | Inbound traffic diagnostics: recent submissions, validation failures, per-producer volume |
| **Routing.Web** | Routing API | Routing decisions per message; dead-lettered/malformed messages |
| **Delivery.Web** | Delivery API | Per-subscriber delivery status, retry queue, manual retry |
| **Archive.Web** | Archive API | Browse/search archived raw payloads by MessageId/date range |
| **Monitoring.Web** | Monitoring API | Default landing dashboard: cross-cutting lifecycle timeline, throughput/failure rates, health |

Each MFE only calls its own service's API. `Monitoring.Web` is the one
dashboard-of-everything view, but still only through Monitoring's own
aggregation API.

### API internals: CQRS (lightweight to start)

Each service's API splits Commands and Queries into separate handler classes
(MediatR), both reading/writing the same database initially — no separate
read model yet. A cache-aside layer (Redis) in front of Queries is a planned
future evolution once a service shows read-heavy load worth caching (likely
candidates: Catalog lookups, Monitoring's aggregated views) — not built in
the initial pass, but the Command/Query split is designed so adding it later
doesn't require restructuring handlers.

### Repo layout

```
AzureSuite.slnx
services/
  Catalog/       → Catalog.Api, Catalog.Domain, Catalog.Infrastructure (+ tests/)
  Ingestion/     → Ingestion.Api, ...
  Routing/       → Routing.Functions, Routing.Domain, ...
  Delivery/      → Delivery.Functions or .Api, ...
  Archive/       → Archive.Functions, ...
  Monitoring/    → Monitoring.Api (aggregator), ...
producers/
  SampleProducer/
frontends/
  Shell/
  Catalog.Web/, Ingestion.Web/, Routing.Web/, Delivery.Web/, Archive.Web/, Monitoring.Web/
infra/
  main.bicep + modules/<service>/  (one module group per service)
docs/
  progress-log.md
```

### Testing & code-quality conventions (carried forward, unchanged)

- Warning-free, nullable-enabled, no `#pragma warning disable` (except
  EF-generated migration files, never hand-edited).
- 1:1 `tests/X.Y.Tests` project per `src`/`services` project, mirroring folder
  structure, one test class per production class.
- xUnit + FluentAssertions + coverlet.collector; hand-written fakes before
  reaching for a mocking library.

### Auth

Deferred. No Entra ID SSO wiring in the initial build — added later "if
required," once the core pipeline (Catalog → Ingestion → Routing → Delivery →
Archive → Monitoring) works end to end. This is a deliberate scope cut, not an
oversight — revisit once the pipeline is proven.

## Build order (small steps, one service at a time)

Dependency order, each step its own design→implement→verify cycle via
`writing-plans`, not all planned upfront:

1. **Catalog** — no dependencies, no messaging, simplest first step.
2. **Ingestion** — depends on Catalog (sync call).
3. **Archive** — simplest consumer of the raw topic, first messaging exercise.
4. **Routing** — depends on Catalog (rules) + publishes onward.
5. **Delivery** — depends on Routing's output.
6. **Monitoring** — depends on data existing in the others.
7. Frontends follow their service (`Catalog.Web` right after Catalog, etc.)
   rather than all backends then all frontends.

## Reset mechanics

- **Repo**: same repo, git history kept (progress-log.md is a useful decision
  journal). All current tracked code/infra deleted and replaced in a fresh
  commit — not a new repo.
- **Azure**: new resource group (old resource names are tied to the payments
  domain, e.g. `sql-azuresuite-dev-pumpkin`; a fresh RG avoids Key Vault
  soft-delete name collisions). Old `rg-azuresuite-dev` and its resources,
  plus the two Entra ID app registrations (`AzureSuite-Api`, `AzureSuite-Web`),
  are deleted once the new RG is confirmed working. Naming convention for the
  new RG/resources to be decided at first infra step (Catalog's Bicep module).

## Out of scope for this pass (explicitly deferred, not forgotten)

- Auth/Entra ID SSO — revisit after core pipeline works.
- Redis caching — revisit once a service shows read-heavy load.
- Full CQRS read model — revisit if/when needed.
- Real external producer/consumer apps — start simulated, swap later.
- API Management, Front Door, Private Endpoints, AI Search — not part of the
  initial 6-service build; candidates for a later phase once the core hub is
  solid.
