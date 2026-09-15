using System.Net;
using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using AzureSuite.Ingestion.Web.Contracts;
using AzureSuite.Ingestion.Web.Services;
using FluentAssertions;
using Xunit;

namespace Ingestion.Web.Tests.Services
{
    public class IngestionApiClientTests
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
        public async Task SubmitMessageAsync_ReturnsDeserializedDto()
        {
            var expected = new IngestedMessageDto(Guid.NewGuid(), "pacs.008", "1.0");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new IngestionApiClient(httpClient);

            var result = await client.SubmitMessageAsync(
                new IngestMessageRequest { MessageType = "pacs.008", Version = "1.0", Payload = "{}" },
                CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task SubmitMessageAsync_ThrowsHttpRequestException_WhenApiReturnsBadRequest()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new IngestionApiClient(httpClient);

            var act = () => client.SubmitMessageAsync(
                new IngestMessageRequest { MessageType = "pacs.008", Version = "1.0", Payload = "{}" },
                CancellationToken.None);

            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
