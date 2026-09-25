using AzureSuite.Web.Blazor.Configuration;
using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class AppFooterTests : BunitContext
{
    [Fact]
    public void RendersALinkToEachApisScalarPageOpeningInANewTab()
    {
        Services.AddSingleton(new ApiReferenceLinks(
        [
            new ApiEndpoint("Catalog API", new Uri("https://localhost:7184")),
            new ApiEndpoint("Ingestion API", new Uri("https://app-ingestion.azurewebsites.net/"))
        ]));

        var cut = Render<AppFooter>();

        var links = cut.FindAll(".app-footer a");
        links.Select(a => a.TextContent).Should().Equal("Catalog API", "Ingestion API");
        links.Select(a => a.GetAttribute("href")).Should().Equal(
            "https://localhost:7184/scalar",
            "https://app-ingestion.azurewebsites.net/scalar");
        links.Should().OnlyContain(a => a.GetAttribute("target") == "_blank");
    }

    [Fact]
    public void RendersNothingWhenThereAreNoApisToLinkTo()
    {
        Services.AddSingleton(new ApiReferenceLinks([]));

        var cut = Render<AppFooter>();

        cut.Markup.Trim().Should().BeEmpty();
    }
}
