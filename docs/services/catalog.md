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
| `Catalog.Api` | Minimal API endpoints, CORS for the web app, Scalar UI at `/scalar/v1` (local Development only), health at `/health` |

## API

| Method | Route | Purpose | Auth |
|---|---|---|---|
| `POST` / `GET` | `/message-types` | Register / list message types | POST `CatalogWrite`, GET `CatalogRead` |
| `GET` | `/message-types/{name}/{version}` | Get one type | `CatalogRead` |
| `POST` | `/message-types/{name}/{version}/validate` | Check a payload against the type's JSON Schema → `{ isValid, errors[] }` | `CatalogRead` |
| `POST` / `GET` | `/clients` | Create / list clients | POST `CatalogWrite`, GET `CatalogRead` |
| `GET` / `PUT` / `DELETE` | `/clients/{id}` | Get / update / delete a client | GET `CatalogRead`, PUT/DELETE `CatalogWrite` |
| `POST` / `GET` | `/routes` | Create / list routes | POST `CatalogWrite`, GET `CatalogRead` |
| `GET` / `PUT` / `DELETE` | `/routes/{id}` | Get / update / delete a route | GET `CatalogRead`, PUT/DELETE `CatalogWrite` |
| `GET` | `/routes/lookup?clientId=&messageType=&version=` | → `{ queueNames[] }`; an empty list means the client isn't authorized | `CatalogRead` |
| `GET` | `/health` | Liveness: runs no checks, so it never touches SQL | anonymous |
| `GET` | `/health/verbose?tags=` | Runs the checks (`db`, `data`) and reports each | any signed-in caller |

`validate` and `routes/lookup` are kept separate on purpose. One checks the payload's shape,
the other checks the client's authorization. Callers can use either one alone.

### Auth

Every endpoint needs an Entra ID access token for `api://<tenantId>/messaginghub-api`. Anything
not listed with a policy is still denied to anonymous callers by default. `CatalogRead` accepts the
`Catalog.Reader` or `Catalog.Admin` role; `CatalogWrite` needs `Catalog.Admin`. No token → 401,
token without the role → 403. Policies live in `services/Shared/AzureSuite.Security`; roles are
defined in [infra/identity/main.bicep](../../infra/identity/main.bicep).

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
