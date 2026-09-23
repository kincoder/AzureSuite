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
