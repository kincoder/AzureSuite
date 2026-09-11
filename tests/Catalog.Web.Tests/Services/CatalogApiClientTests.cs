using System.Net;
using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Contracts;
using AzureSuite.Catalog.Web.Services;
using FluentAssertions;
using Xunit;

namespace Catalog.Web.Tests.Services
{
    public class CatalogApiClientTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_respond(request));
            }
        }

        [Fact]
        public async Task GetMessageTypesAsync_ReturnsDeserializedList()
        {
            var expected = new List<MessageTypeDto>
            {
                new(Guid.NewGuid(), "pacs.008", "1.0", "{}", DateTime.UtcNow)
            };
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var result = await client.GetMessageTypesAsync(CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task RegisterMessageTypeAsync_ReturnsDeserializedDto()
        {
            var expected = new MessageTypeDto(Guid.NewGuid(), "camt.054", "1.0", "{}", DateTime.UtcNow);
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var result = await client.RegisterMessageTypeAsync(
                new RegisterMessageTypeRequest { Name = "camt.054", Version = "1.0", SchemaDefinition = "{}" },
                CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task GetMessageTypesAsync_ThrowsHttpRequestException_WhenApiReturnsServerError()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var act = () => client.GetMessageTypesAsync(CancellationToken.None);

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task RegisterMessageTypeAsync_ThrowsHttpRequestException_WhenApiReturnsConflict()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new CatalogApiClient(httpClient);

            var act = () => client.RegisterMessageTypeAsync(
                new RegisterMessageTypeRequest { Name = "camt.054", Version = "1.0", SchemaDefinition = "{}" },
                CancellationToken.None);

            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
