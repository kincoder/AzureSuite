# Catalog.Web (Micro-Frontend) — Design

## Why this piece, now

Per the messaging hub design (`2026-09-11-financial-messaging-hub-design.md`), frontends
are built as genuinely separate micro-frontends, one per bounded context, and are built
*right after* their corresponding backend rather than all backends first. Catalog's
backend is done and verified end-to-end; this is the first micro-frontend, and the first
real test of the MFE composition mechanism the earlier design only specified in principle.

## Framework and composition mechanism

**Blazor WebAssembly**, chosen to keep the whole solution in C# (accepted trade-off: MFE
composition tooling for Blazor is less mature than the JS ecosystem, but staying
single-language was preferred over a more battle-tested option).

**Composition: Custom Elements + dynamic module loading.** Each micro-frontend (starting
with Catalog.Web) is built as its own Blazor WASM app whose root component is exposed as a
Custom Element via `Microsoft.AspNetCore.Components.CustomElements` (officially supported
by Microsoft since .NET 8, not a community package). A later Shell app loads each MFE's
compiled JS bundle at runtime and drops a custom element tag (e.g. `<catalog-app>`) into
the page. This keeps each MFE independently buildable and deployable — the Shell has no
compile-time dependency on Catalog.Web.

## This build's scope: Catalog.Web only

Catalog.Web talks directly to Catalog.Api's existing endpoints — nothing speculative:

- **List view**: calls `GET /message-types`, renders registered message types.
- **Register form**: calls `POST /message-types` (name, version, schema definition).

No subscriber/routing-rule management UI yet — Catalog.Api has no endpoints for those
either; building UI ahead of the backend risks rework once that's designed.

The Shell is explicitly **out of scope for this build** — Catalog.Web ships first as a
standalone-runnable app (its own `index.html`) to prove the custom-element approach works
against real content, and only then does a minimal Shell get built to load it. Building the
Shell against a placeholder MFE first was considered and rejected: composing something real
is a better test of the mechanism than composing a placeholder.

## Data source for local development

Catalog.Api already falls back to an EF Core InMemory database whenever no `CatalogDb`
connection string is configured (built during the Catalog service work). This build adds
**seed data** to that fallback path: 2-3 sample message types (e.g. `pacs.008`/`1.0`,
`camt.054`/`1.0`) inserted on startup when running InMemory, so Catalog.Web development
never requires Azure SQL to be running. The real Azure SQL path (`rg-messaginghub-dev`,
`sql-messaginghub-catalog-dev`) is untouched and remains the production/verification path —
its serverless free-tier database costs nothing at current usage, this change is purely a
local-dev convenience, not a cost-driven replacement.

CORS: Catalog.Api needs to allow Catalog.Web's local dev origin (and later its deployed
Static Web App origin) to call it — not currently configured, needs adding.

## Hosting

**Azure Static Web Apps** — purpose-built for static SPA hosting (Blazor WASM compiles to
static files), free tier, built-in CDN. One Static Web App per MFE (plus one for the Shell,
later). Catalog.Web's: `stapp-messaginghub-catalog-dev`, provisioned via Bicep in the same
`rg-messaginghub-dev` resource group, following the established
`<type>-messaginghub-<service>-dev` naming convention.

## Repo layout

```
frontends/
  Catalog.Web/     → this build
  Shell/           → future build, once Catalog.Web is solid
```

## Build order

1. Catalog.Api: add InMemory seed data (2-3 sample message types) + CORS for local dev.
2. Catalog.Web project scaffold: list + register pages calling the real Catalog.Api,
   verified running standalone locally (own `index.html`, no custom element yet).
3. Expose the root component as a Custom Element (`<catalog-app>`), still
   standalone-runnable, proving the mechanism works.
4. Static Web App Bicep infra + deploy, verified against the real deployed Catalog.Api.
5. Shell — separate future piece, not part of this build.

## Out of scope for this pass

- The Shell / cross-MFE composition — next piece after this one, not bundled in.
- Subscriber/routing-rule management UI — waits on the corresponding Catalog.Api endpoints.
- Auth — still deferred per the overall messaging hub design, revisit once the core
  pipeline (not just Catalog) is proven end to end.
