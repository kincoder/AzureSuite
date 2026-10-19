using AzureSuite.Ingestion.Api;
using AzureSuite.Ingestion.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ingestion.Api.Tests
{
    /// <summary>Test host for <see cref="Program"/> that substitutes a no-op fake publisher
    /// in place of ServiceBusMessagePublisher, so integration tests never touch a real Service
    /// Bus namespace — mirrors CatalogApiFactory's InMemory-database substitution.</summary>
    public class IngestionApiFactory : WebApplicationFactory<Program>
    {
        public const string TestApiClientId = "11111111-2222-3333-4444-555555555555";

        private sealed class FakeMessagePublisher : IMessagePublisher
        {
            public Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public IngestionApiFactory()
        {
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Name", "EventLog");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__source", "Ingestion.Api");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__manageEventSource", "false");

            // A fixed API client id, so JwtAudienceTests can check real token validation against it.
            Environment.SetEnvironmentVariable("AzureAd__ClientId", TestApiClientId);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessagePublisher>();
                services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();
            });

            // ConfigureTestServices runs after Program's registrations, so these defaults win over
            // the JWT bearer scheme Microsoft.Identity.Web registered; that scheme is never invoked.
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                });
            });
        }

        /// <summary>A client whose requests are authenticated with exactly these roles.</summary>
        public HttpClient CreateClientWithRoles(params string[] roles)
        {
            var client = CreateClient();
            // HttpClient drops empty header values, which would make the caller anonymous; a lone
            // comma still means "signed in with no roles" once split.
            var value = string.Join(',', roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, value.Length == 0 ? "," : value);
            return client;
        }
    }
}
