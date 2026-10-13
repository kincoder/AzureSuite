using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage;
using AzureSuite.Processing.Infrastructure.Messaging;
using AzureSuite.Processing.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Processing.Functions
{
    /// <summary>Composition root for the Processing Function App (isolated worker). Both
    /// triggers live in this one app so they share one identity, one set of role assignments,
    /// and one deployment.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = FunctionsApplication.CreateBuilder(args);

            builder.Services
                .AddApplicationInsightsTelemetryWorkerService()
                .ConfigureFunctionsApplicationInsights();

            // The App Insights worker package adds a default rule that only forwards Warning and
            // above; remove it so this app's Information logs (one per message, one per
            // housekeeping run) reach App Insights too.
            builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
            {
                var defaultRule = options.Rules.FirstOrDefault(rule =>
                    rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                if (defaultRule is not null)
                {
                    options.Rules.Remove(defaultRule);
                }
            });

            var configuration = builder.Configuration;

            // Same credential choice as Ingestion.Api: Managed Identity is only reachable on
            // Azure, and probing it locally fails hard instead of falling through to the
            // Azure CLI / Visual Studio credentials.
            TokenCredential credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment()
            });

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RecordInboundMessageCommand).Assembly));
            builder.Services.Configure<HousekeepingOptions>(configuration.GetSection(HousekeepingOptions.SectionName));

            builder.Services.AddSingleton(_ => new ServiceBusClient(
                Required(configuration, "ServiceBus:fullyQualifiedNamespace"), credential));
            builder.Services.AddSingleton<IDeadLetterQueue>(sp => new ServiceBusDeadLetterQueue(
                sp.GetRequiredService<ServiceBusClient>(), Required(configuration, "InboundQueueName")));

            builder.Services.AddSingleton(_ => new CosmosClient(
                Required(configuration, "Cosmos:AccountEndpoint"), credential, CosmosLifecycleEventStore.CreateClientOptions()));
            builder.Services.AddSingleton<ILifecycleEventStore>(sp => new CosmosLifecycleEventStore(
                sp.GetRequiredService<CosmosClient>().GetContainer(
                    Required(configuration, "Cosmos:DatabaseName"),
                    Required(configuration, "Cosmos:ContainerName"))));

            builder.Build().Run();
        }

        private static string Required(IConfiguration configuration, string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Configuration value '{key}' is missing.");
            }

            return value;
        }
    }
}
