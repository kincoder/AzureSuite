using System.Net;
using AzureSuite.Observability.HealthChecks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AzureSuite.Observability.Tests.HealthChecks;

public class HealthCheckEndpointExtensionsTests
{
    private static async Task<(TestServer Server, HttpClient Client)> CreateServerAsync(HealthStatus checkStatus)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHealthChecks()
                        .AddCheck("fake", () => new HealthCheckResult(checkStatus, "fake check description"), tags: ["fake-tag"]);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAzureSuiteHealthChecks());
                });
            });

        var host = await hostBuilder.StartAsync();
        var server = host.GetTestServer();
        return (server, server.CreateClient());
    }

    [Fact]
    public async Task MinimalEndpoint_WhenHealthy_ReturnsOkWithStatusOnly()
    {
        var (_, client) = await CreateServerAsync(HealthStatus.Healthy);

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("{\"status\":\"Healthy\"}");
    }

    [Fact]
    public async Task MinimalEndpoint_WhenUnhealthy_ReturnsServiceUnavailable()
    {
        var (_, client) = await CreateServerAsync(HealthStatus.Unhealthy);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task VerboseEndpoint_IncludesPerCheckDetails()
    {
        var (_, client) = await CreateServerAsync(HealthStatus.Healthy);

        var response = await client.GetAsync("/health/verbose");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"name\":\"fake\"");
        body.Should().Contain("\"description\":\"fake check description\"");
        body.Should().Contain("\"tags\":[\"fake-tag\"]");
    }

    [Fact]
    public async Task TagsQueryString_RunsOnlyMatchingChecks()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHealthChecks()
                        .AddCheck("data-check", () => HealthCheckResult.Healthy(), tags: ["data"])
                        .AddCheck("db-check", () => HealthCheckResult.Unhealthy(), tags: ["db"]);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAzureSuiteHealthChecks());
                });
            });
        var host = await hostBuilder.StartAsync();
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/health/verbose?tags=data");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"name\":\"data-check\"");
        body.Should().NotContain("db-check");
    }
}
