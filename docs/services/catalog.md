# Catalog

**Where:** [services/Catalog/](../../services/Catalog/) · **Runs on:** App Service
`app-messaginghub-catalog-dev` (Linux, F1) · **Data:** Azure SQL `sql-messaginghub-catalog-dev` /
database `catalog`

## What it solves

The hub can only work with messages it knows about. Catalog is the reference data for every
other service. It answers three questions:

- **Which message types exist?** Each type has a name, a version and a JSON Schema
  (`MessageType`).
- **Who can send?** The registered senders (`Client`).
- **Where does a message go?** A `Route` maps (client, message type, version) to one or more
  output queue names. If no route exists, that client isn't allowed to send that type.

Catalog is static data. It never changes by itself, only through these endpoints.

## Layers

| Project | Responsibility |
|---|---|
| `Catalog.Domain` | Entities `MessageType`, `Client`, `Route`; value objects `MessageTypeName`, `MessageTypeVersion`. Rules live here. |
| `Catalog.Application` | MediatR commands and queries, one folder each under `{Clients,Routes,MessageTypes}/{Commands,Queries}/<Name>/` |
| `Catalog.Infrastructure` | EF Core `CatalogDbContext`, migrations, repositories |
| `Catalog.Api` | Minimal API endpoints, CORS for the web app, Scalar UI at `/scalar/v1`, health at `/health` |

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` / `GET` | `/message-types` | Register / list message types |
| `GET` | `/message-types/{name}/{version}` | Get one type |
| `POST` | `/message-types/{name}/{version}/validate` | Check a payload against the type's JSON Schema → `{ isValid, errors[] }` |
| `POST` / `GET` | `/clients` | Create / list clients |
| `GET` / `PUT` / `DELETE` | `/clients/{id}` | Get / update / delete a client |
| `POST` / `GET` | `/routes` | Create / list routes |
| `GET` / `PUT` / `DELETE` | `/routes/{id}` | Get / update / delete a route |
| `GET` | `/routes/lookup?clientId=&messageType=&version=` | → `{ queueNames[] }`; an empty list means the client isn't authorized |

`validate` and `routes/lookup` are kept separate on purpose. One checks the payload's shape,
the other checks the client's authorization. Callers can use either one alone.

## Running locally

- Connection string `ConnectionStrings:CatalogDb` goes in user-secrets on `Catalog.Api` and
  points at LocalDB.
- Apply migrations: `dotnet ef database update --project services/Catalog/Catalog.Infrastructure --startup-project services/Catalog/Catalog.Api`
- Optional seed data: [scripts/seed-routing-test-data.sql](../../scripts/seed-routing-test-data.sql)
- Runs at `https://localhost:7184`.

## Deployment

The `deploy-catalog-api` job in [build.yml](../../.github/workflows/build.yml) builds an EF
migration bundle. It opens the SQL firewall for the runner, applies the migrations, closes the
firewall, and zip-deploys the API. The connection string comes from Key Vault
`kv-msghub-catalog-dev` through a Key Vault reference, read with the App Service's managed
identity.

## Gotchas

- Map every property in `OnModelCreating` explicitly. EF once silently dropped a get-only
  property from a migration. Always read generated migrations before applying them.
- Integration tests use `CatalogApiFactory`, which swaps SQL Server for InMemory. Without it,
  tests write to the real database.
