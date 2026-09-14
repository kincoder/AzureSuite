using AzureSuite.Ingestion.Api;
using AzureSuite.Ingestion.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ingestion.Api.Tests
{
    /// <summary>Test host for <see cref="Program"/> that substitutes a no-op fake publisher
    /// in place of ServiceBusMessagePublisher, so integration tests never touch a real Service
    /// Bus namespace — mirrors CatalogApiFactory's InMemory-database substitution.</summary>
    public class IngestionApiFactory : WebApplicationFactory<Program>
    {
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
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessagePublisher>();
                services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();
            });
        }
    }
}
