using System.Net;
using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using FluentAssertions;
using Xunit;

namespace Ingestion.Api.Tests
{
    public class MessagesEndpointTests : IClassFixture<IngestionApiFactory>
    {
        private readonly HttpClient _client;

        public MessagesEndpointTests(IngestionApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Post_WithAllFieldsPresent_ReturnsCreatedWithMessageId()
        {
            var response = await _client.PostAsJsonAsync("/messages", new
            {
                messageType = "pacs.008",
                version = "1.0",
                payload = "{}"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var dto = await response.Content.ReadFromJsonAsync<IngestedMessageDto>();
            dto!.MessageId.Should().NotBeEmpty();
            dto.MessageType.Should().Be("pacs.008");
            dto.Version.Should().Be("1.0");
        }

        [Theory]
        [InlineData("", "1.0", "{}")]
        [InlineData("pacs.008", "", "{}")]
        [InlineData("pacs.008", "1.0", "")]
        public async Task Post_WithAMissingField_ReturnsBadRequest(string messageType, string version, string payload)
        {
            var response = await _client.PostAsJsonAsync("/messages", new { messageType, version, payload });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var response = await _client.GetAsync("/health");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
