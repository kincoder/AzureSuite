using AzureSuite.Ingestion.Api.Contracts;
using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage;
using AzureSuite.Ingestion.Infrastructure.Messaging;
using AzureSuite.Observability;
using AzureSuite.Observability.HealthChecks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using MediatR;
using Scalar.AspNetCore;

namespace AzureSuite.Ingestion.Api
{
    /// <summary>Entry point and endpoint registration for the Ingestion service's HTTP API.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddAzureSuiteLogging("Ingestion.Api");

            builder.Services.AddOpenApi();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IngestMessageCommand).Assembly));

            builder.Services.AddSingleton(sp =>
            {
                var fullyQualifiedNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"];
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    // Managed Identity is only reachable when running on Azure; probing it locally
                    // throws AuthenticationFailedException (not CredentialUnavailableException),
                    // which aborts the credential chain before it reaches AzureCliCredential.
                    ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment()
                });
                return new ServiceBusClient(fullyQualifiedNamespace, credential);
            });
            builder.Services.AddSingleton<IMessagePublisher>(sp =>
            {
                var client = sp.GetRequiredService<ServiceBusClient>();
                var queueName = builder.Configuration["ServiceBus:QueueName"]!;
                return new ServiceBusMessagePublisher(client, queueName);
            });

            builder.Services.AddHealthChecks();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(
                            "https://localhost:7205",
                            "http://localhost:5147")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            app.MapAzureSuiteHealthChecks();

            app.MapPost("/messages", async (IngestMessageRequest request, IMediator mediator, ILogger<Program> logger) =>
            {
                logger.LogInformation("Received submission for type {MessageType} v{Version}", request.MessageType, request.Version);

                if (string.IsNullOrWhiteSpace(request.MessageType) ||
                    string.IsNullOrWhiteSpace(request.Version) ||
                    string.IsNullOrWhiteSpace(request.Payload))
                {
                    logger.LogWarning(
                        "Rejected submission for type {MessageType} v{Version}: a required field was missing",
                        request.MessageType, request.Version);
                    return Results.BadRequest("messageType, version, and payload are all required.");
                }

                var dto = await mediator.Send(new IngestMessageCommand(request.MessageType, request.Version, request.Payload));
                return Results.Created($"/messages/{dto.MessageId}", dto);
            });

            app.Run();
        }
    }
}
