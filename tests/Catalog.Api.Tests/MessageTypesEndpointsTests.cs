using System.Net;
using System.Net.Http.Json;
using AzureSuite.Catalog.Api;
using AzureSuite.Catalog.Application.MessageTypes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Catalog.Api.Tests
{
    public class MessageTypesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public MessageTypesEndpointsTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RegisterThenGet_ReturnsTheRegisteredMessageType()
        {
            var registerResponse = await _client.PostAsJsonAsync("/message-types", new
            {
                name = "pacs.008",
                version = "1.0",
                schemaDefinition = "{}"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var registered = await registerResponse.Content.ReadFromJsonAsync<MessageTypeDto>();

            var getResponse = await _client.GetAsync("/message-types/pacs.008/1.0");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var fetched = await getResponse.Content.ReadFromJsonAsync<MessageTypeDto>();

            fetched!.Id.Should().Be(registered!.Id);
        }

        [Fact]
        public async Task Get_WhenMessageTypeDoesNotExist_ReturnsNotFound()
        {
            var response = await _client.GetAsync("/message-types/does-not-exist/1.0");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task List_ReturnsAllRegisteredMessageTypes()
        {
            await _client.PostAsJsonAsync("/message-types", new { name = "pacs.008", version = "2.0", schemaDefinition = "{}" });
            await _client.PostAsJsonAsync("/message-types", new { name = "camt.054", version = "1.0", schemaDefinition = "{}" });

            var response = await _client.GetAsync("/message-types");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var all = await response.Content.ReadFromJsonAsync<List<MessageTypeDto>>();
            all!.Select(m => m.Name).Should().Contain(new[] { "pacs.008", "camt.054" });
        }
    }
}
