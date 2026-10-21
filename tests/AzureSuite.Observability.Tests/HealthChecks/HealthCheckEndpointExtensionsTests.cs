using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AzureSuite.Observability.HealthChecks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AzureSuite.Observability.Tests.HealthChecks;

public class HealthCheckEndpointExtensionsTests
{
    private const string SignedInHeader = "X-Test-Signed-In";

    private static async Task<HttpClient> CreateClientAsync(Action<IHealthChecksBuilder> addChecks)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(SignedInHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, SignedInHandler>(SignedInHandler.SchemeName, null);
                    services.AddAuthorization();
                    addChecks(services.AddHealthChecks());
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapAzureSuiteHealthChecks());
                });
            });

        var host = await hostBuilder.StartAsync();
        return host.GetTestServer().CreateClient();
    }

    private static Task<HttpClient> CreateClientAsync(HealthStatus checkStatus) =>
        CreateClientAsync(checks => checks.AddCheck("fake", () => new HealthCheckResult(checkStatus, "fake check description"), tags: ["fake-tag"]));

    private static HttpRequestMessage SignedIn(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(SignedInHeader, "true");
        return request;
    }

    [Fact]
    public async Task MinimalEndpoint_WhenHealthy_ReturnsOkWithStatusOnly()
    {
        var client = await CreateClientAsync(HealthStatus.Healthy);

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("{\"status\":\"Healthy\"}");
    }

    [Fact]
    public async Task MinimalEndpoint_RunsNoChecks_SoAnonymousTrafficNeverReachesDependencies()
    {
        var checkRuns = 0;
        var client = await CreateClientAsync(checks => checks.AddCheck("db", () =>
        {
            checkRuns++;
            return HealthCheckResult.Unhealthy();
        }, tags: ["db"]));

        var response = await client.GetAsync("/health?tags=db");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        checkRuns.Should().Be(0);
    }

    [Fact]
    public async Task VerboseEndpoint_Anonymous_Returns401()
    {
        var client = await CreateClientAsync(HealthStatus.Healthy);

        var response = await client.GetAsync("/health/verbose");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerboseEndpoint_SignedIn_IncludesPerCheckDetails()
    {
        var client = await CreateClientAsync(HealthStatus.Healthy);

        var response = await client.SendAsync(SignedIn("/health/verbose"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"name\":\"fake\"");
        body.Should().Contain("\"description\":\"fake check description\"");
        body.Should().Contain("\"tags\":[\"fake-tag\"]");
    }

    [Fact]
    public async Task VerboseEndpoint_SignedIn_WhenUnhealthy_ReturnsServiceUnavailable()
    {
        var client = await CreateClientAsync(HealthStatus.Unhealthy);

        var response = await client.SendAsync(SignedIn("/health/verbose"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task VerboseEndpoint_TagsQueryString_RunsOnlyMatchingChecks()
    {
        var client = await CreateClientAsync(checks => checks
            .AddCheck("data-check", () => HealthCheckResult.Healthy(), tags: ["data"])
            .AddCheck("db-check", () => HealthCheckResult.Unhealthy(), tags: ["db"]));

        var response = await client.SendAsync(SignedIn("/health/verbose?tags=data"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"name\":\"data-check\"");
        body.Should().NotContain("db-check");
    }

    /// <summary>Authenticates any request carrying <see cref="SignedInHeader"/>; others stay anonymous.</summary>
    private sealed class SignedInHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public SignedInHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(SignedInHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
