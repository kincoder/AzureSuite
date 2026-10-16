using System.Net;
using System.Net.Http.Json;
using AzureSuite.Security;
using FluentAssertions;

namespace Ingestion.Api.Tests
{
    public class AuthorizationTests : IClassFixture<IngestionApiFactory>
    {
        private static readonly object ValidMessage = new { messageType = "pacs.008", version = "1.0", payload = "{}" };
        private readonly IngestionApiFactory _factory;

        public AuthorizationTests(IngestionApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostMessages_Anonymous_Returns401()
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogAdmin)]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData("")]
        public async Task PostMessages_WithoutSubmitRole_Returns403(string role)
        {
            var response = await _factory.CreateClientWithRoles(role).PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task PostMessages_WithSubmitRole_Returns201()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.MessagesSubmit).PostAsJsonAsync("/messages", ValidMessage);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/verbose")]
        public async Task Health_Anonymous_Returns200(string path)
        {
            var response = await _factory.CreateClient().GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CorsPreflightFromTheWebApp_WithoutToken_IsAllowed()
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/messages");
            request.Headers.Add("Origin", "https://delightful-sky-05bade503.2.azurestaticapps.net");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

            var response = await _factory.CreateClient().SendAsync(request);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Headers.GetValues("Access-Control-Allow-Origin").Should()
                .Contain("https://delightful-sky-05bade503.2.azurestaticapps.net");
        }
    }
}
