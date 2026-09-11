# Shared MFE UI Theme & Catalog.Web Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a shared Blazor Razor Class Library (`AzureSuite.Web.UI`) carrying a dark/antique-gold design system (CSS tokens + themed components), then restyle Catalog.Web's two pages to use it — table becomes a card grid with skeleton loading, form gets themed inputs.

**Architecture:** New RCL project `frontends/AzureSuite.Web.UI` ships `wwwroot/theme.css` (plain CSS custom properties, no preprocessor) plus small parameter-driven `.razor` components (`PageHeader`, `AppCard`, `AppButton`, `AppBadge`, `SkeletonCard`, `EmptyState`, `ErrorState`). Catalog.Web references it as a normal ProjectReference and links `theme.css` + Google Fonts `Inter` from `index.html`. No backend, contract, or `.razor.cs` logic changes anywhere — this plan is visual-only.

**Tech Stack:** Blazor WebAssembly (.NET 10), Razor Class Library, plain CSS custom properties, bUnit 2.10.3 for component tests, xUnit + FluentAssertions (existing repo convention).

**Spec:** `docs/superpowers/specs/2026-09-11-shared-ui-theme-design.md`

## Global Constraints

- Target framework `net10.0` everywhere, `Nullable` + `ImplicitUsings` enabled — matches every existing project in this repo.
- Palette (exact hex, from spec): bg `#12100E`, surface `#1C1917`, surface-raised `#242019`, border `#332C22`, text `#F3EFE8`, text-muted `#A8A096`, accent `#C9A86A`, accent-hover `#DDBF89`, error `#E5A4A0`.
- Typography: `Inter` only, loaded via Google Fonts `<link>` (same CDN-allowlist pattern already used in this project).
- No icon library, no JS, no CSS preprocessor — plain CSS custom properties only (spec: "Out of scope").
- No behavior changes to `MessageTypesList.razor.cs` / `RegisterMessageType.razor.cs` / `CatalogApiClient` — styling and markup only.
- New test project follows this repo's 1:1 `X.Y.Tests` convention (`docs/progress-log.md` / memory: warning-free code, 1:1 test project structure).
- Every new/modified `.csproj` must be added to `AzureSuite.slnx` under the matching `/frontends/` or `/tests/` folder.

---

### Task 1: Scaffold `AzureSuite.Web.UI` RCL with theme tokens

**Files:**
- Create: `frontends/AzureSuite.Web.UI/AzureSuite.Web.UI.csproj`
- Create: `frontends/AzureSuite.Web.UI/_Imports.razor`
- Create: `frontends/AzureSuite.Web.UI/wwwroot/theme.css`
- Modify: `AzureSuite.slnx` (add project to `/frontends/` folder)

**Interfaces:**
- Produces: CSS custom properties consumed by every component in Tasks 2-3 and by Catalog.Web's pages in Task 5 (`--color-*`, `--space-*`, `--radius-*`, `--shadow-card`, `--font-family`), plus utility classes `.eyebrow`, `.page-header`, `.app-card`, `.card-grid`, `.app-badge`, `.app-button`, `.app-button-primary`, `.app-button-secondary`, `.skeleton-card`, `.skeleton-line`, `.empty-state`, `.error-state`, `.form-group`.

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AzureSuite.Web.UI</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Include="browser" />
  </ItemGroup>

</Project>
```

Save as `frontends/AzureSuite.Web.UI/AzureSuite.Web.UI.csproj`.

- [ ] **Step 2: Create `_Imports.razor`**

```razor
@using Microsoft.AspNetCore.Components
```

Save as `frontends/AzureSuite.Web.UI/_Imports.razor`.

- [ ] **Step 3: Create `theme.css` with the full token set and utility classes**

```css
:root {
    --color-bg: #12100E;
    --color-surface: #1C1917;
    --color-surface-raised: #242019;
    --color-border: #332C22;
    --color-text: #F3EFE8;
    --color-text-muted: #A8A096;
    --color-accent: #C9A86A;
    --color-accent-hover: #DDBF89;
    --color-error: #E5A4A0;

    --space-1: 4px;
    --space-2: 8px;
    --space-3: 12px;
    --space-4: 16px;
    --space-5: 24px;
    --space-6: 32px;
    --space-7: 48px;
    --space-8: 64px;

    --radius-card: 12px;
    --radius-control: 8px;

    --shadow-card: 0 4px 24px rgba(0, 0, 0, 0.35);

    --font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}

body {
    background: var(--color-bg);
    color: var(--color-text);
    font-family: var(--font-family);
    margin: 0;
    padding: var(--space-6);
}

.eyebrow {
    text-transform: uppercase;
    letter-spacing: 0.1em;
    font-size: 0.75rem;
    color: var(--color-text-muted);
}

.page-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    margin-bottom: var(--space-6);
    gap: var(--space-4);
    flex-wrap: wrap;
}

.page-header h1 {
    font-size: 1.75rem;
    font-weight: 600;
    letter-spacing: -0.01em;
    margin: var(--space-1) 0 0;
}

.page-header .subtitle {
    color: var(--color-text-muted);
    margin-top: var(--space-1);
}

.app-card {
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-card);
    box-shadow: var(--shadow-card);
    padding: var(--space-5);
    transition: background-color 0.15s ease;
}

.app-card:hover {
    background: var(--color-surface-raised);
}

.card-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    gap: var(--space-4);
}

.app-badge {
    display: inline-block;
    padding: 2px 10px;
    border-radius: 999px;
    background: rgba(201, 168, 106, 0.12);
    color: var(--color-accent);
    font-size: 0.8rem;
    font-weight: 500;
}

.app-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-control);
    font-weight: 600;
    cursor: pointer;
    border: 1px solid transparent;
    text-decoration: none;
    font-family: inherit;
    font-size: 0.95rem;
    transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}

.app-button-primary {
    background: var(--color-accent);
    color: #16130F;
}

.app-button-primary:hover {
    background: var(--color-accent-hover);
}

.app-button-secondary {
    background: transparent;
    color: var(--color-text);
    border-color: var(--color-border);
}

.app-button-secondary:hover {
    border-color: var(--color-accent);
    color: var(--color-accent);
}

.skeleton-card {
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-card);
    padding: var(--space-5);
}

.skeleton-line {
    height: 14px;
    border-radius: 4px;
    margin-bottom: var(--space-3);
    background: linear-gradient(90deg, var(--color-border) 25%, var(--color-surface-raised) 50%, var(--color-border) 75%);
    background-size: 200% 100%;
    animation: shimmer 1.4s ease-in-out infinite;
}

.skeleton-line:last-child {
    margin-bottom: 0;
}

@keyframes shimmer {
    0% { background-position: 200% 0; }
    100% { background-position: -200% 0; }
}

.empty-state, .error-state {
    text-align: center;
    padding: var(--space-8) var(--space-4);
    color: var(--color-text-muted);
}

.error-state {
    color: var(--color-error);
}

.form-group {
    margin-bottom: var(--space-4);
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    max-width: 480px;
}

.form-group label {
    font-size: 0.85rem;
    color: var(--color-text-muted);
}

.form-group input,
.form-group textarea {
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-control);
    color: var(--color-text);
    padding: var(--space-2) var(--space-3);
    font-family: inherit;
    font-size: 0.95rem;
}

.form-group textarea {
    min-height: 120px;
    resize: vertical;
}

.form-group input:focus,
.form-group textarea:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 3px rgba(201, 168, 106, 0.2);
}
```

Save as `frontends/AzureSuite.Web.UI/wwwroot/theme.css`.

- [ ] **Step 4: Register the project in the solution**

Open `AzureSuite.slnx` and add the new project inside the existing `/frontends/` folder, alongside `Catalog.Web.csproj`:

```xml
  <Folder Name="/frontends/">
    <Project Path="frontends/AzureSuite.Web.UI/AzureSuite.Web.UI.csproj" />
    <Project Path="frontends/Catalog.Web/Catalog.Web.csproj" />
  </Folder>
```

- [ ] **Step 5: Verify the project builds**

Run: `dotnet build frontends/AzureSuite.Web.UI/AzureSuite.Web.UI.csproj`
Expected: `Build succeeded`, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add frontends/AzureSuite.Web.UI AzureSuite.slnx
git commit -m "$(cat <<'EOF'
Add AzureSuite.Web.UI shared theme RCL with design tokens

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Layout components — `PageHeader`, `AppCard`, `AppBadge`

**Files:**
- Create: `frontends/AzureSuite.Web.UI/Components/PageHeader.razor`
- Create: `frontends/AzureSuite.Web.UI/Components/AppCard.razor`
- Create: `frontends/AzureSuite.Web.UI/Components/AppBadge.razor`
- Test: `tests/AzureSuite.Web.UI.Tests/AzureSuite.Web.UI.Tests.csproj`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/PageHeaderTests.cs`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/AppCardTests.cs`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/AppBadgeTests.cs`

**Interfaces:**
- Consumes: nothing (pure presentation components).
- Produces:
  - `PageHeader`: parameters `Title` (`string`, required), `Eyebrow` (`string?`), `Subtitle` (`string?`), `Actions` (`RenderFragment?`).
  - `AppCard`: parameter `ChildContent` (`RenderFragment?`).
  - `AppBadge`: parameter `ChildContent` (`RenderFragment?`).
  - All live in namespace `AzureSuite.Web.UI.Components` (folder-based Razor namespace under `RootNamespace` = `AzureSuite.Web.UI`).

- [ ] **Step 1: Create the bUnit test project**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.10.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\frontends\AzureSuite.Web.UI\AzureSuite.Web.UI.csproj" />
  </ItemGroup>

</Project>
```

Save as `tests/AzureSuite.Web.UI.Tests/AzureSuite.Web.UI.Tests.csproj`.

Add it to `AzureSuite.slnx` inside `/tests/`:

```xml
    <Project Path="tests/AzureSuite.Web.UI.Tests/AzureSuite.Web.UI.Tests.csproj" />
```

- [ ] **Step 2: Write the failing test for `PageHeader`**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class PageHeaderTests : TestContext
{
    [Fact]
    public void RendersTitleEyebrowAndSubtitle()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Registered Message Types")
            .Add(p => p.Eyebrow, "Catalog")
            .Add(p => p.Subtitle, "All message types currently registered"));

        cut.Find("h1").TextContent.Should().Be("Registered Message Types");
        cut.Find(".eyebrow").TextContent.Should().Be("Catalog");
        cut.Find(".subtitle").TextContent.Should().Be("All message types currently registered");
    }

    [Fact]
    public void RendersActionsFragmentWhenProvided()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Register Message Type")
            .Add(p => p.Actions, "<button>Go</button>"));

        cut.Find("button").TextContent.Should().Be("Go");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/PageHeaderTests.cs`.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter PageHeaderTests`
Expected: FAIL — compile error, `PageHeader` does not exist.

- [ ] **Step 4: Implement `PageHeader.razor`**

```razor
<div class="page-header">
    <div>
        @if (!string.IsNullOrWhiteSpace(Eyebrow))
        {
            <div class="eyebrow">@Eyebrow</div>
        }
        <h1>@Title</h1>
        @if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            <div class="subtitle">@Subtitle</div>
        }
    </div>
    @if (Actions is not null)
    {
        <div>@Actions</div>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string? Eyebrow { get; set; }

    [Parameter]
    public string? Subtitle { get; set; }

    [Parameter]
    public RenderFragment? Actions { get; set; }
}
```

Save as `frontends/AzureSuite.Web.UI/Components/PageHeader.razor`.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter PageHeaderTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Write the failing test for `AppCard`**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class AppCardTests : TestContext
{
    [Fact]
    public void RendersChildContentInsideCardDiv()
    {
        var cut = RenderComponent<AppCard>(parameters => parameters
            .AddChildContent("<p>pacs.008</p>"));

        var card = cut.Find(".app-card");
        card.QuerySelector("p")!.TextContent.Should().Be("pacs.008");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/AppCardTests.cs`.

- [ ] **Step 7: Run it, confirm it fails, then implement `AppCard.razor`**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppCardTests`
Expected: FAIL — `AppCard` does not exist.

```razor
<div class="app-card">
    @ChildContent
</div>

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

Save as `frontends/AzureSuite.Web.UI/Components/AppCard.razor`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppCardTests`
Expected: PASS.

- [ ] **Step 8: Write the failing test for `AppBadge`, then implement it**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class AppBadgeTests : TestContext
{
    [Fact]
    public void RendersChildContentInsideBadgeSpan()
    {
        var cut = RenderComponent<AppBadge>(parameters => parameters
            .AddChildContent("1.0"));

        cut.Find(".app-badge").TextContent.Should().Be("1.0");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/AppBadgeTests.cs`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppBadgeTests`
Expected: FAIL.

```razor
<span class="app-badge">
    @ChildContent
</span>

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

Save as `frontends/AzureSuite.Web.UI/Components/AppBadge.razor`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppBadgeTests`
Expected: PASS.

- [ ] **Step 9: Run the full test project and commit**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests`
Expected: PASS, all tests green, 0 warnings on build.

```bash
git add frontends/AzureSuite.Web.UI/Components tests/AzureSuite.Web.UI.Tests AzureSuite.slnx
git commit -m "$(cat <<'EOF'
Add PageHeader, AppCard, AppBadge components with bUnit tests

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Interactive & state components — `AppButton`, `SkeletonCard`, `EmptyState`, `ErrorState`

**Files:**
- Create: `frontends/AzureSuite.Web.UI/Components/AppButtonVariant.cs`
- Create: `frontends/AzureSuite.Web.UI/Components/AppButton.razor`
- Create: `frontends/AzureSuite.Web.UI/Components/SkeletonCard.razor`
- Create: `frontends/AzureSuite.Web.UI/Components/EmptyState.razor`
- Create: `frontends/AzureSuite.Web.UI/Components/ErrorState.razor`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/AppButtonTests.cs`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/SkeletonCardTests.cs`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/EmptyStateTests.cs`
- Test: `tests/AzureSuite.Web.UI.Tests/Components/ErrorStateTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `AppButtonVariant` enum: `Primary`, `Secondary`.
  - `AppButton`: parameters `ChildContent` (`RenderFragment?`), `Href` (`string?`), `Type` (`string`, default `"button"`), `Disabled` (`bool`), `Variant` (`AppButtonVariant`, default `Primary`). Renders `<a>` when `Href` is set, otherwise `<button>`.
  - `SkeletonCard`: no parameters.
  - `EmptyState` / `ErrorState`: parameter `Message` (`string`, required).

- [ ] **Step 1: Create `AppButtonVariant.cs`**

```csharp
namespace AzureSuite.Web.UI.Components;

/// <summary>Visual weight of an <see cref="AppButton"/>.</summary>
public enum AppButtonVariant
{
    Primary,
    Secondary
}
```

Save as `frontends/AzureSuite.Web.UI/Components/AppButtonVariant.cs`.

- [ ] **Step 2: Write the failing tests for `AppButton`**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class AppButtonTests : TestContext
{
    [Fact]
    public void RendersButtonElementByDefaultWithPrimaryClass()
    {
        var cut = RenderComponent<AppButton>(parameters => parameters
            .AddChildContent("Register"));

        var button = cut.Find("button");
        button.TextContent.Should().Be("Register");
        button.ClassList.Should().Contain("app-button-primary");
        button.GetAttribute("type").Should().Be("button");
    }

    [Fact]
    public void RendersAnchorElementWhenHrefIsSet()
    {
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Href, "/register")
            .AddChildContent("Register a new message type"));

        var anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Be("/register");
        anchor.TextContent.Should().Be("Register a new message type");
    }

    [Fact]
    public void RendersSecondaryClassWhenVariantIsSecondary()
    {
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Variant, AppButtonVariant.Secondary)
            .AddChildContent("Cancel"));

        cut.Find("button").ClassList.Should().Contain("app-button-secondary");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/AppButtonTests.cs`.

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppButtonTests`
Expected: FAIL — `AppButton` does not exist.

- [ ] **Step 4: Implement `AppButton.razor`**

```razor
@if (Href is not null)
{
    <a href="@Href" class="app-button @VariantClass">@ChildContent</a>
}
else
{
    <button type="@Type" class="app-button @VariantClass" disabled="@Disabled">@ChildContent</button>
}

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public string Type { get; set; } = "button";

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public AppButtonVariant Variant { get; set; } = AppButtonVariant.Primary;

    private string VariantClass => Variant == AppButtonVariant.Primary ? "app-button-primary" : "app-button-secondary";
}
```

Save as `frontends/AzureSuite.Web.UI/Components/AppButton.razor`.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter AppButtonTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Write the failing test for `SkeletonCard`, then implement it**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class SkeletonCardTests : TestContext
{
    [Fact]
    public void RendersThreeShimmerLinesInsideSkeletonCard()
    {
        var cut = RenderComponent<SkeletonCard>();

        cut.Find(".skeleton-card").QuerySelectorAll(".skeleton-line").Should().HaveCount(3);
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/SkeletonCardTests.cs`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter SkeletonCardTests`
Expected: FAIL.

```razor
<div class="skeleton-card">
    <div class="skeleton-line" style="width:60%"></div>
    <div class="skeleton-line" style="width:40%"></div>
    <div class="skeleton-line" style="width:80%"></div>
</div>
```

Save as `frontends/AzureSuite.Web.UI/Components/SkeletonCard.razor`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter SkeletonCardTests`
Expected: PASS.

- [ ] **Step 7: Write the failing tests for `EmptyState` and `ErrorState`, then implement them**

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class EmptyStateTests : TestContext
{
    [Fact]
    public void RendersMessageInsideEmptyStateDiv()
    {
        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Message, "No message types registered yet."));

        cut.Find(".empty-state").TextContent.Should().Be("No message types registered yet.");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/EmptyStateTests.cs`.

```csharp
using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class ErrorStateTests : TestContext
{
    [Fact]
    public void RendersMessageInsideErrorStateDiv()
    {
        var cut = RenderComponent<ErrorState>(parameters => parameters
            .Add(p => p.Message, "Failed to load message types."));

        cut.Find(".error-state").TextContent.Should().Be("Failed to load message types.");
    }
}
```

Save as `tests/AzureSuite.Web.UI.Tests/Components/ErrorStateTests.cs`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter "EmptyStateTests|ErrorStateTests"`
Expected: FAIL — types don't exist.

```razor
<div class="empty-state">@Message</div>

@code {
    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;
}
```

Save as `frontends/AzureSuite.Web.UI/Components/EmptyState.razor`.

```razor
<div class="error-state">@Message</div>

@code {
    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;
}
```

Save as `frontends/AzureSuite.Web.UI/Components/ErrorState.razor`.

Run: `dotnet test tests/AzureSuite.Web.UI.Tests --filter "EmptyStateTests|ErrorStateTests"`
Expected: PASS.

- [ ] **Step 8: Run the full test suite and commit**

Run: `dotnet test tests/AzureSuite.Web.UI.Tests`
Expected: PASS, all tests green, 0 warnings.

```bash
git add frontends/AzureSuite.Web.UI/Components tests/AzureSuite.Web.UI.Tests
git commit -m "$(cat <<'EOF'
Add AppButton, SkeletonCard, EmptyState, ErrorState components

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Wire `AzureSuite.Web.UI` into Catalog.Web

**Files:**
- Modify: `frontends/Catalog.Web/Catalog.Web.csproj`
- Modify: `frontends/Catalog.Web/_Imports.razor`
- Modify: `frontends/Catalog.Web/wwwroot/index.html`

**Interfaces:**
- Consumes: `AzureSuite.Web.UI` project output (components under `AzureSuite.Web.UI.Components`, static asset `theme.css`).
- Produces: nothing new — this task only wires the dependency so Task 5 can use the components.

- [ ] **Step 1: Add the project reference**

In `frontends/Catalog.Web/Catalog.Web.csproj`, add a second `ProjectReference` next to the existing one:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\services\Catalog\Catalog.Application\Catalog.Application.csproj" />
    <ProjectReference Include="..\AzureSuite.Web.UI\AzureSuite.Web.UI.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Add the `@using` for the shared components**

In `frontends/Catalog.Web/_Imports.razor`, add a line after the existing `@using AzureSuite.Catalog.Web`:

```razor
@using AzureSuite.Web.UI.Components
```

- [ ] **Step 3: Link the font and theme stylesheet in `index.html`**

Replace the `<head>` block in `frontends/Catalog.Web/wwwroot/index.html`:

```html
<head>
    <meta charset="utf-8" />
    <title>Catalog</title>
    <base href="/" />
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <link href="_content/AzureSuite.Web.UI/theme.css" rel="stylesheet" />
</head>
```

- [ ] **Step 4: Verify Catalog.Web still builds with the new reference**

Run: `dotnet build frontends/Catalog.Web/Catalog.Web.csproj`
Expected: `Build succeeded`, 0 warnings, 0 errors. Confirms the RCL's static web assets and components are visible to Catalog.Web.

- [ ] **Step 5: Commit**

```bash
git add frontends/Catalog.Web/Catalog.Web.csproj frontends/Catalog.Web/_Imports.razor frontends/Catalog.Web/wwwroot/index.html
git commit -m "$(cat <<'EOF'
Wire Catalog.Web to AzureSuite.Web.UI theme and font

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Restyle `MessageTypesList` as a skeleton-loading card grid

**Files:**
- Modify: `frontends/Catalog.Web/Pages/MessageTypesList.razor`
- Test: `tests/Catalog.Web.Tests/Pages/MessageTypesListRenderTests.cs`

**Interfaces:**
- Consumes: `PageHeader`, `AppCard`, `AppBadge`, `SkeletonCard`, `EmptyState`, `ErrorState`, `AppButton`, `AppButtonVariant` from `AzureSuite.Web.UI.Components` (Task 2/3). Existing `MessageTypesList.razor.cs` properties `MessageTypes` (`IReadOnlyList<MessageTypeDto>?`), `ErrorMessage` (`string?`) — unchanged.
- Produces: nothing consumed by later tasks.

First, check whether `Catalog.Web.Tests` already references bUnit — it doesn't (it's an xUnit project testing `CatalogApiClient`/contracts, not rendering). This task adds bUnit to it so the existing markup states can be verified by rendering, following the same pattern as Task 2's tests.

- [ ] **Step 1: Add bUnit to `Catalog.Web.Tests`**

In `tests/Catalog.Web.Tests/Catalog.Web.Tests.csproj`, add to the existing `PackageReference` `ItemGroup`:

```xml
    <PackageReference Include="bunit" Version="2.10.3" />
```

- [ ] **Step 2: Write the failing test for the three list states**

```csharp
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Pages;
using AzureSuite.Catalog.Web.Services;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Catalog.Web.Tests.Pages;

public class MessageTypesListRenderTests : TestContext
{
    public MessageTypesListRenderTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
    }

    [Fact]
    public void RendersSixSkeletonCardsWhileLoading()
    {
        // CatalogApiClient's GetMessageTypesAsync call against a fake base address never
        // resolves within the render window, so the component stays in its loading state.
        var cut = RenderComponent<MessageTypesList>();

        cut.FindAll(".skeleton-card").Should().HaveCount(6);
    }
}
```

Save as `tests/Catalog.Web.Tests/Pages/MessageTypesListRenderTests.cs`.

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/Catalog.Web.Tests --filter MessageTypesListRenderTests`
Expected: FAIL — no `.skeleton-card` elements exist yet (current page renders `<p>Loading...</p>`).

- [ ] **Step 4: Replace `MessageTypesList.razor` markup**

```razor
@page "/"

<PageHeader Title="Registered Message Types" Eyebrow="Catalog">
    <Actions>
        <AppButton Href="/register" Variant="AppButtonVariant.Primary">Register a new message type</AppButton>
    </Actions>
</PageHeader>

@if (ErrorMessage is not null)
{
    <ErrorState Message="@ErrorMessage" />
}
else if (MessageTypes is null)
{
    <div class="card-grid">
        @for (var i = 0; i < 6; i++)
        {
            <SkeletonCard />
        }
    </div>
}
else if (MessageTypes.Count == 0)
{
    <EmptyState Message="No message types registered yet." />
}
else
{
    <div class="card-grid">
        @foreach (var messageType in MessageTypes)
        {
            <AppCard>
                <h3>@messageType.Name</h3>
                <AppBadge>@messageType.Version</AppBadge>
                <p class="subtitle">Registered @messageType.RegisteredAtUtc</p>
            </AppCard>
        }
    </div>
}
```

Save as `frontends/Catalog.Web/Pages/MessageTypesList.razor`. `MessageTypesList.razor.cs` is unchanged — same `MessageTypes`/`ErrorMessage` properties, same `OnInitializedAsync` logic.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Catalog.Web.Tests --filter MessageTypesListRenderTests`
Expected: PASS.

- [ ] **Step 6: Run the full Catalog.Web.Tests suite to confirm no regressions**

Run: `dotnet test tests/Catalog.Web.Tests`
Expected: PASS, all existing tests plus the new one green, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add tests/Catalog.Web.Tests/Catalog.Web.Tests.csproj tests/Catalog.Web.Tests/Pages/MessageTypesListRenderTests.cs frontends/Catalog.Web/Pages/MessageTypesList.razor
git commit -m "$(cat <<'EOF'
Restyle MessageTypesList as a skeleton-loading card grid

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Restyle `RegisterMessageType` form

**Files:**
- Modify: `frontends/Catalog.Web/Pages/RegisterMessageType.razor`
- Test: `tests/Catalog.Web.Tests/Pages/RegisterMessageTypeRenderTests.cs`

**Interfaces:**
- Consumes: `PageHeader`, `AppButton`, `ErrorState` from `AzureSuite.Web.UI.Components`. Existing `RegisterMessageType.razor.cs` properties `Request` (`RegisterMessageTypeRequest`), `ErrorMessage` (`string?`), `SubmittedMessage` (`string?`), method `SubmitAsync` — unchanged.
- Produces: nothing consumed by later tasks (last task).

- [ ] **Step 1: Write the failing test for the themed form shell**

```csharp
using AzureSuite.Catalog.Web.Pages;
using AzureSuite.Catalog.Web.Services;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Catalog.Web.Tests.Pages;

public class RegisterMessageTypeRenderTests : TestContext
{
    public RegisterMessageTypeRenderTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
    }

    [Fact]
    public void RendersThreeFormGroupsAndAPrimarySubmitButton()
    {
        var cut = RenderComponent<RegisterMessageType>();

        cut.FindAll(".form-group").Should().HaveCount(3);
        var submit = cut.Find("button[type=submit]");
        submit.ClassList.Should().Contain("app-button-primary");
        submit.TextContent.Should().Be("Register");
    }
}
```

Save as `tests/Catalog.Web.Tests/Pages/RegisterMessageTypeRenderTests.cs`.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Catalog.Web.Tests --filter RegisterMessageTypeRenderTests`
Expected: FAIL — no `.form-group` elements or `app-button-primary` class exist yet.

- [ ] **Step 3: Replace `RegisterMessageType.razor` markup**

```razor
@page "/register"

<PageHeader Title="Register Message Type" Eyebrow="Catalog" />

<EditForm Model="@Request" OnValidSubmit="@SubmitAsync">
    <div class="form-group">
        <label>Name</label>
        <InputText @bind-Value="Request.Name" />
    </div>
    <div class="form-group">
        <label>Version</label>
        <InputText @bind-Value="Request.Version" />
    </div>
    <div class="form-group">
        <label>Schema Definition</label>
        <InputTextArea @bind-Value="Request.SchemaDefinition" />
    </div>
    <AppButton Type="submit">Register</AppButton>
</EditForm>

@if (ErrorMessage is not null)
{
    <ErrorState Message="@ErrorMessage" />
}

@if (SubmittedMessage is not null)
{
    <p class="subtitle">@SubmittedMessage</p>
}
```

Save as `frontends/Catalog.Web/Pages/RegisterMessageType.razor`. `RegisterMessageType.razor.cs` is unchanged.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Catalog.Web.Tests --filter RegisterMessageTypeRenderTests`
Expected: PASS.

- [ ] **Step 5: Run the full solution test suite**

Run: `dotnet test`
Expected: every test project (including `Catalog.Api.Tests`, `Catalog.Application.Tests`, `Catalog.Domain.Tests`, `Catalog.Infrastructure.Tests`, `Catalog.Web.Tests`, `AzureSuite.Web.UI.Tests`) PASS, 0 warnings across the solution.

- [ ] **Step 6: Manually verify in the browser**

Run: `dotnet run --project frontends/Catalog.Web`
Open the served URL. Confirm:
- `/` shows the dark/gold theme, 6 shimmering skeleton cards appear briefly then are replaced by real cards in a responsive grid (resize the window narrow to confirm it reflows to one column).
- `/register` shows the themed dark form with a gold submit button; submitting a valid message type shows a success message and redirecting back to `/` shows it in the grid.
- Font is `Inter` (check via browser dev tools computed styles) and colors match the palette (background `#12100E`, accent `#C9A86A`).

- [ ] **Step 7: Commit**

```bash
git add tests/Catalog.Web.Tests/Pages/RegisterMessageTypeRenderTests.cs frontends/Catalog.Web/Pages/RegisterMessageType.razor
git commit -m "$(cat <<'EOF'
Restyle RegisterMessageType form with themed inputs and button

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Post-plan note

Shell and Ingestion MFEs (out of scope here, per spec) pick up the same look later by adding a `ProjectReference` to `AzureSuite.Web.UI` and the same two `<link>` tags to their own `index.html` — no changes to this plan's output are needed when that happens.
