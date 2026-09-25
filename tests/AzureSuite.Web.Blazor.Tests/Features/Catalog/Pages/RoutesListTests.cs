using System.Net;
using System.Net.Http.Json;
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages
{
    public class RoutesListTests : BunitContext
    {
        private sealed class FakeCatalogApiHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(respond(request));
        }

        private void UseApi(IReadOnlyList<ClientDto> clients, IReadOnlyList<RouteDto> routes)
        {
            var handler = new FakeCatalogApiHandler(request => request.RequestUri!.AbsolutePath switch
            {
                "/clients" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(clients) },
                "/routes" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(routes) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });
            Services.AddScoped(_ => new CatalogApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }));
        }

        [Fact]
        public void RendersHeading()
        {
            UseApi(Array.Empty<ClientDto>(), Array.Empty<RouteDto>());

            var cut = Render<RoutesList>();

            cut.Find("h1").TextContent.Should().Be("Routes");
        }

        [Fact]
        public void ShowsTheClientNameInsteadOfItsId()
        {
            var contoso = new ClientDto(Guid.NewGuid(), "Contoso Payments", DateTime.UtcNow);
            UseApi(new[] { contoso }, new[] { new RouteDto(Guid.NewGuid(), contoso.Id, "pacs.008", "1.0", new[] { "out-settlements" }) });

            var cut = Render<RoutesList>();

            cut.WaitForAssertion(() => cut.Find(".data-table tbody td").TextContent.Should().Be("Contoso Payments"));
        }

        [Fact]
        public void FallsBackToTheClientIdWhenTheClientIsNotInTheList()
        {
            var unknownClientId = Guid.NewGuid();
            UseApi(Array.Empty<ClientDto>(), new[] { new RouteDto(Guid.NewGuid(), unknownClientId, "pacs.008", "1.0", new[] { "out-settlements" }) });

            var cut = Render<RoutesList>();

            cut.WaitForAssertion(() => cut.Find(".data-table tbody td").TextContent.Should().Be(unknownClientId.ToString()));
        }
    }
}
