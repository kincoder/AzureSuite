using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages
{
    public class ClientsListTests : BunitContext
    {
        public ClientsListTests()
        {
            Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        }

        [Fact]
        public void RendersHeading()
        {
            var cut = Render<ClientsList>();

            cut.Find("h1").TextContent.Should().Be("Clients");
        }
    }
}
