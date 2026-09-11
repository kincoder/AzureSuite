# Shared MFE UI Theme & Catalog.Web Visual Redesign — Design

## Why this piece, now

Catalog.Web is functionally complete (`2026-09-11-catalog-web-design.md`) but visually
undifferentiated — unstyled table, no loading feedback beyond a text string. More MFEs
(Shell, Ingestion) are coming per the messaging hub design, and each will need the same
look and feel. Building a one-off stylesheet for Catalog.Web now would mean redoing this
work per MFE. This piece establishes a shared visual language once, applies it to
Catalog.Web, and leaves it ready for Shell/Ingestion to reference later.

## Approach: shared Razor Class Library

A new project, `AzureSuite.Web.UI` (`frontends/AzureSuite.Web.UI/AzureSuite.Web.UI.csproj`),
a Razor Class Library referenced by Catalog.Web (and, later, every other Blazor WASM MFE)
as a normal project reference. It ships:

- **Design tokens** as CSS custom properties (`wwwroot/theme.css`) — color palette,
  typography scale, spacing scale, radius, shadow/elevation values. No JS, no build-time
  CSS preprocessing — plain CSS custom properties keep it framework-agnostic and trivially
  consumable by any MFE, including ones that aren't Blazor if that ever happens.
- **Themed components** (`.razor` + scoped or shared CSS): `PageHeader`, `AppCard`,
  `AppButton`, `AppBadge`, `SkeletonCard`, `EmptyState`, `ErrorState`. Each is a small,
  self-contained component with no dependency on Catalog-specific types — they take
  primitives (strings, `RenderFragment`s, booleans) as parameters.

This was chosen over (a) a CSS-only shared stylesheet linked via `<link>`, and (b) a
combined RCL+CSS package, per explicit direction: a shared component library was preferred
over CSS-only because it gives every MFE actual reusable Blazor components (consistent
markup, not just consistent colors), accepting the added coupling of a shared project
reference across MFEs as worthwhile for a small internal solution.

Why not fold this into Catalog.Web itself: Shell and Ingestion will need the same
components: a name like `AzureSuite.Web.UI` signals it's cross-MFE from day one, avoiding
an extraction step later.

## Visual direction

Dark, premium aesthetic with a restrained antique-gold accent (chosen per approved
direction: "golden stuff" interpreted as a refined, muted gold rather than a bright/garish
yellow, to read as premium enterprise software rather than a casino).

**Palette** (CSS custom properties in `theme.css`):
- `--color-bg`: `#12100E` (near-black charcoal, warm undertone)
- `--color-surface`: `#1C1917` (card backgrounds, one step up from bg)
- `--color-surface-raised`: `#242019` (hover/active surface state)
- `--color-border`: `#332C22` (subtle warm-toned borders)
- `--color-text`: `#F3EFE8` (warm off-white, not pure white)
- `--color-text-muted`: `#A8A096`
- `--color-accent`: `#C9A86A` (antique gold — primary actions, active states, highlights)
- `--color-accent-hover`: `#DDBF89`
- `--color-error`: `#E5A4A0` (muted rose, not a harsh red, to stay in the same warm family)

**Typography:** `Inter` for all UI text (body, labels, buttons) loaded via Google Fonts
`<link>` in each MFE's `index.html` (consistent with the CDN-allowlist pattern used
elsewhere in this project); page titles use a slightly larger weight of the same family
with letter-spacing rather than a second display font, keeping the type system to one
family for simplicity. Small uppercase "eyebrow" labels (e.g. section labels) use
letter-spacing and `--color-text-muted` for a refined feel.

**Spacing/shape:** 8px base spacing scale (`--space-1` through `--space-8`), `12px` card
corner radius, soft low-opacity box-shadows for elevation instead of hard borders where
possible (borders reserved for input fields and dividers).

## Component behavior

- **`PageHeader`**: title + optional subtitle/eyebrow text + optional trailing
  `RenderFragment` (for action buttons like "Register new"). Used on both Catalog.Web
  pages.
- **`AppCard`**: generic bordered/elevated container, used both for real content cards and
  as the base shape skeletons mimic (so skeleton and loaded states share identical
  dimensions — no layout shift).
- **`SkeletonCard`**: same footprint as `AppCard`, contents replaced with shimmering
  placeholder blocks (CSS `@keyframes` gradient animation, no JS). `MessageTypesList` shows
  6 of these in the grid while `OnInitializedAsync` is awaiting the API call.
- **`EmptyState`** / **`ErrorState`**: centered icon-free message blocks (icon-free to
  avoid pulling in an icon font/library dependency for this first pass), consistent
  typography, used to replace today's bare `<p>` messages.
- **`AppButton`**: styled `<button>`/`<a>` wrapper — primary (gold-filled) and secondary
  (outlined) variants; `RegisterMessageType`'s submit button and `MessageTypesList`'s
  "Register a new message type" link both use it.

## Catalog.Web page changes

**`MessageTypesList`** (`/`): table replaced with a responsive CSS Grid of `AppCard`s
(`repeat(auto-fill, minmax(260px, 1fr))`, so it reflows from multi-column to single-column
on narrow viewports without a media query). Each card shows the message type name as its
title, version as an `AppBadge`, and the registered timestamp as muted secondary text.
Three states, matching current `MessageTypesList.razor.cs` logic exactly (no behavior
change, styling only):
1. Loading (`MessageTypes is null`) → grid of 6 `SkeletonCard`s.
2. Error (`ErrorMessage is not null`) → `ErrorState`.
3. Empty (`Count == 0`) → `EmptyState` ("No message types registered yet.").
4. Loaded → grid of `AppCard`s.

**`RegisterMessageType`** (`/register`): existing `EditForm`/`InputText`/`InputTextArea`
fields keep their current bindings and validation logic; only wrapped in themed styling
(labeled form groups, dark-styled inputs with gold focus ring, `AppButton` submit). Success
and error messages use the same muted-text/error-text treatment as the list page for
consistency.

## Out of scope

- No changes to `CatalogApiClient`, `Program.cs`, contracts, or any backend/API behavior.
- No new pages, routes, or fields.
- No icon library / illustration assets — kept to typography, color, and shape for this
  first pass; can be revisited later if a specific need arises.
- Shell and Ingestion MFEs don't exist yet — this spec only builds the shared library and
  proves it out in Catalog.Web; wiring it into future MFEs happens when those MFEs are
  built, by adding the same project reference and `<link>` tags.

## Testing

`AzureSuite.Web.UI` components get basic bUnit rendering tests (renders expected markup
for given parameters) under a new `tests/AzureSuite.Web.UI.Tests` project, matching this
repo's 1:1 test-project convention. `Catalog.Web.Tests` existing tests are unaffected since
no `.razor.cs` logic changes; if any new tests are warranted for the restyled pages
(e.g. confirming skeleton count while loading), they're added there.
