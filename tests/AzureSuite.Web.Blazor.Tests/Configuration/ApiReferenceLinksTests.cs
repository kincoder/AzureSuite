using AzureSuite.Web.Blazor.Configuration;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.Configuration;

public class ApiReferenceLinksTests
{
    private static readonly ApiEndpoint[] Apis =
    [
        new("Catalog API", new Uri("https://localhost:7184")),
        new("Ingestion API", new Uri("https://localhost:7185"))
    ];

    [Fact]
    public void ServedBy_InDevelopment_LinksEveryApi()
    {
        var links = ApiReferenceLinks.ServedBy(Apis, isDevelopment: true);

        links.Apis.Should().Equal(Apis);
    }

    [Fact]
    public void ServedBy_OutsideDevelopment_LinksNothing()
    {
        var links = ApiReferenceLinks.ServedBy(Apis, isDevelopment: false);

        links.Apis.Should().BeEmpty();
    }
}
