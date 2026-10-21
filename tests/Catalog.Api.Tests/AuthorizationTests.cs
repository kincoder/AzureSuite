using System.Net;
using System.Net.Http.Json;
using AzureSuite.Security;
using FluentAssertions;

namespace Catalog.Api.Tests
{
    public class AuthorizationTests : IClassFixture<CatalogApiFactory>
    {
        private readonly CatalogApiFactory _factory;

        public AuthorizationTests(CatalogApiFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/message-types")]
        [InlineData("/clients")]
        [InlineData("/routes")]
        public async Task Reads_Anonymous_Return401(string path)
        {
            var response = await _factory.CreateClient().GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData(AppRoles.CatalogAdmin)]
        public async Task Reads_WithReaderOrAdmin_Return200(string role)
        {
            var client = _factory.CreateClientWithRoles(role);

            (await client.GetAsync("/message-types")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/clients")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/routes")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RouteLookup_WithReader_Returns200()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogReader)
                .GetAsync($"/routes/lookup?clientId={Guid.NewGuid()}&messageType=pacs.008&version=1.0");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Validate_WithReader_IsNotForbidden()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogReader)
                .PostAsJsonAsync("/message-types/pacs.008/1.0/validate", new { payload = "{}" });

            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AppRoles.CatalogReader)]
        [InlineData(AppRoles.MessagesSubmit)]
        [InlineData("")]
        public async Task Writes_WithoutAdmin_Return403(string role)
        {
            var client = _factory.CreateClientWithRoles(role);

            (await client.PostAsJsonAsync("/clients", new { name = "Intruder" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostAsJsonAsync("/message-types", new { name = "x", version = "1.0", schemaDefinition = "{}" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.DeleteAsync($"/routes/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Writes_Anonymous_Return401()
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/clients", new { name = "Intruder" });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateClient_WithAdmin_Returns201()
        {
            var response = await _factory.CreateClientWithRoles(AppRoles.CatalogAdmin)
                .PostAsJsonAsync("/clients", new { name = $"Client {Guid.NewGuid()}" });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task CorsPreflightFromTheWebApp_WithoutToken_IsAllowed()
        {
            var request = new HttpRequestMessage(HttpMethod.Options, $"/clients/{Guid.NewGuid()}");
            request.Headers.Add("Origin", "https://delightful-sky-05bade503.2.azurestaticapps.net");
            request.Headers.Add("Access-Control-Request-Method", "PUT");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

            var response = await _factory.CreateClient().SendAsync(request);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Headers.GetValues("Access-Control-Allow-Origin").Should()
                .Contain("https://delightful-sky-05bade503.2.azurestaticapps.net");
        }

        [Fact]
        public async Task Health_Anonymous_Returns200()
        {
            var response = await _factory.CreateClient().GetAsync("/health?tags=db");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task HealthVerbose_Anonymous_Returns401()
        {
            var response = await _factory.CreateClient().GetAsync("/health/verbose");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task HealthVerbose_SignedInWithoutRoles_IsNotUnauthorized()
        {
            // The catalog-populated check may report Unhealthy (503) on an empty test database;
            // the point here is only that any signed-in caller gets past authorization.
            var response = await _factory.CreateClientWithRoles("").GetAsync("/health/verbose?tags=db");

            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }
    }
}
