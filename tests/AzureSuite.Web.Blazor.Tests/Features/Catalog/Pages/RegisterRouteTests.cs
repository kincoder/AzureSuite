using System.Net;
using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages
{
    public class RegisterRouteTests : BunitContext
    {
        private sealed class FakeCatalogApiHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(respond(request));
        }

        private static readonly ClientDto Contoso = new(Guid.NewGuid(), "Contoso Payments", DateTime.UtcNow);
        private static readonly MessageTypeDto Pacs008 = new(Guid.NewGuid(), "pacs.008", "1.0", "{}", DateTime.UtcNow);
        private static readonly MessageTypeDto Camt054 = new(Guid.NewGuid(), "camt.054", "1.0", "{}", DateTime.UtcNow);

        private CreateRouteRequest? _postedRequest;

        private void UseApi(IReadOnlyList<ClientDto> clients, IReadOnlyList<MessageTypeDto> messageTypes, Func<HttpResponseMessage>? postRouteResponse = null)
        {
            var handler = new FakeCatalogApiHandler(request =>
            {
                switch (request.RequestUri!.AbsolutePath)
                {
                    case "/clients":
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(clients) };
                    case "/message-types":
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(messageTypes) };
                    case "/routes" when request.Method == HttpMethod.Post:
                        _postedRequest = request.Content!.ReadFromJsonAsync<CreateRouteRequest>().GetAwaiter().GetResult();
                        return postRouteResponse?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.Created)
                        {
                            Content = JsonContent.Create(new RouteDto(Guid.NewGuid(), _postedRequest!.ClientId, _postedRequest.MessageTypeName, _postedRequest.MessageTypeVersion, _postedRequest.QueueNames))
                        };
                    default:
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            });
            Services.AddScoped(_ => new CatalogApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }));
        }

        [Fact]
        public void ListsRegisteredClientsAndMessageTypesAsOptions()
        {
            UseApi(new[] { Contoso }, new[] { Pacs008, Camt054 });

            var cut = Render<RegisterRoute>();

            cut.WaitForAssertion(() =>
            {
                cut.FindAll("#route-client option").Select(o => o.TextContent).Should().Equal("Select a client...", "Contoso Payments");
                cut.FindAll("#route-message-type option").Select(o => o.TextContent).Should().Equal("Select a message type...", "camt.054 (v1.0)", "pacs.008 (v1.0)");
            });
        }

        [Fact]
        public void Submit_WithSelections_PostsChosenClientAndMessageType()
        {
            UseApi(new[] { Contoso }, new[] { Pacs008, Camt054 });
            var cut = Render<RegisterRoute>();
            cut.WaitForElement("#route-client");

            cut.Find("#route-client").Change(Contoso.Id.ToString());
            cut.Find("#route-message-type").Change(Pacs008.Id.ToString());
            cut.Find("#route-queues").Change("out-a, out-b");
            cut.Find("form").Submit();

            cut.WaitForAssertion(() => _postedRequest.Should().NotBeNull());
            _postedRequest!.ClientId.Should().Be(Contoso.Id);
            _postedRequest.MessageTypeName.Should().Be("pacs.008");
            _postedRequest.MessageTypeVersion.Should().Be("1.0");
            _postedRequest.QueueNames.Should().Equal("out-a", "out-b");
        }

        [Fact]
        public void Submit_WithoutSelections_ShowsErrorAndDoesNotPost()
        {
            UseApi(new[] { Contoso }, new[] { Pacs008 });
            var cut = Render<RegisterRoute>();
            cut.WaitForElement("#route-client");

            cut.Find("#route-queues").Change("out-a");
            cut.Find("form").Submit();

            cut.Find(".error-state").TextContent.Should().Be("Choose a client and a message type.");
            _postedRequest.Should().BeNull();
        }

        [Fact]
        public void Submit_WhenApiRejects_ShowsApiMessage()
        {
            UseApi(new[] { Contoso }, new[] { Pacs008 }, () => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create($"Client '{Contoso.Id}' does not exist.")
            });
            var cut = Render<RegisterRoute>();
            cut.WaitForElement("#route-client");

            cut.Find("#route-client").Change(Contoso.Id.ToString());
            cut.Find("#route-message-type").Change(Pacs008.Id.ToString());
            cut.Find("#route-queues").Change("out-a");
            cut.Find("form").Submit();

            cut.WaitForAssertion(() => cut.Find(".error-state").TextContent.Should().Be($"Client '{Contoso.Id}' does not exist."));
        }

        [Fact]
        public void WhenNoMessageTypesRegistered_ShowsHintLinkingToRegistration()
        {
            UseApi(new[] { Contoso }, Array.Empty<MessageTypeDto>());

            var cut = Render<RegisterRoute>();

            cut.WaitForAssertion(() => cut.Find(".form-hint a").GetAttribute("href").Should().Be("/catalog/register"));
        }
    }
}
