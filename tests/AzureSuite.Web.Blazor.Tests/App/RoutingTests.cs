using AzureSuite.Security;
using AzureSuite.Web.Blazor.Configuration;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using AzureSuite.Web.Blazor.Features.Ingestion.Services;
using AzureSuite.Web.Blazor.UI;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using AppRoot = AzureSuite.Web.Blazor.App;

namespace AzureSuite.Web.Blazor.Tests.App;

public class RoutingTests : BunitContext
{
    public RoutingTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        Services.AddScoped(_ => new ClientTelemetryLogger(JSInterop.JSRuntime));
        Services.AddSingleton(new ApiReferenceLinks([]));

        // The fake base address never resolves, so MessageTypesList/SubmitMessage's catch
        // block calls ClientTelemetry.LogExceptionAsync -- without this, bUnit's strict
        // JSInterop mode throws on that unconfigured call, which crashes into
        // AppErrorBoundary and wipes the whole page (no h1 at all). Timing-dependent: only
        // surfaces if the async HTTP failure resolves before the test's assertion runs,
        // which is exactly why this passed locally but failed on CI.
        JSInterop.SetupVoid("logException", _ => true);

        var auth = AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetPolicies(AppPolicies.CatalogRead, AppPolicies.CatalogWrite, AppPolicies.SubmitMessages);
    }

    [Fact]
    public void ASignedInUserWhoseApiTokenExpired_IsOfferedToSignInAgain()
    {
        using var context = new BunitContext();
        context.Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new ClientTelemetryLogger(context.JSInterop.JSRuntime));
        context.Services.AddSingleton(new ApiReferenceLinks([]));
        var auth = context.AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetClaims(new System.Security.Claims.Claim(AzureSuite.Web.Blazor.Security.ApiRolesClaimsPrincipalFactory.ApiTokenUnavailableClaim, "true"));

        var cut = context.Render<AppRoot>();

        cut.Markup.Should().NotContain("You don't have access to this page");
        cut.Find("main a[href='authentication/login']").TextContent.Should().Contain("Sign in again");
    }

    [Fact]
    public void ASignedInUserWithoutTheRole_SeesAccessDenied()
    {
        using var context = new BunitContext();
        context.Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        context.Services.AddScoped(_ => new ClientTelemetryLogger(context.JSInterop.JSRuntime));
        context.Services.AddSingleton(new ApiReferenceLinks([]));
        context.AddAuthorization().SetAuthorized("Nobody");

        var cut = context.Render<AppRoot>();

        cut.Markup.Should().Contain("You don't have access to this page");
        cut.Find(".app-nav").Should().NotBeNull();
    }

    [Fact]
    public void RendersTheCatalogListAtTheRoot()
    {
        var cut = Render<AppRoot>();

        cut.Find("h1").TextContent.Should().Be("Registered Message Types");
    }

    [Fact]
    public void RendersPageNotFoundForAnUnknownRoute()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/some-unknown-path");

        var cut = Render<AppRoot>();

        cut.Markup.Should().Contain("Page not found");
        cut.Find(".app-nav").Should().NotBeNull();
    }

    [Fact]
    public void NavigatingToIngestionRendersTheSubmitForm()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/ingestion");

        var cut = Render<AppRoot>();

        cut.Find("h1").TextContent.Should().Be("Submit Message");
    }
}
