using AzureSuite.Catalog.Api.Contracts;
using AzureSuite.Catalog.Api.Persistence;
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Observability;
using AzureSuite.Catalog.Application.Clients.Commands.CreateClient;
using AzureSuite.Catalog.Application.Clients.Commands.UpdateClient;
using AzureSuite.Catalog.Application.Clients.Commands.DeleteClient;
using AzureSuite.Catalog.Application.Clients.Queries.GetClient;
using AzureSuite.Catalog.Application.Clients.Queries.ListClients;
using AzureSuite.Catalog.Application.Routes.Commands.CreateRoute;
using AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute;
using AzureSuite.Catalog.Application.Routes.Commands.DeleteRoute;
using AzureSuite.Catalog.Application.Routes.Queries.GetRoute;
using AzureSuite.Catalog.Application.Routes.Queries.ListRoutes;
using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.HealthChecks;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using AzureSuite.Observability.HealthChecks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace AzureSuite.Catalog.Api
{
    /// <summary>Entry point and endpoint registration for the Catalog service's HTTP API.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddAzureSuiteLogging("Catalog.Api");

            builder.Services.AddOpenApi();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterMessageTypeCommand).Assembly));
            builder.Services.AddScoped<IMessageTypeRepository, MessageTypeRepository>();
            builder.Services.AddScoped<IClientRepository, ClientRepository>();
            builder.Services.AddScoped<IRouteRepository, RouteRepository>();

            builder.Services.AddHealthChecks()
                .AddDbContextCheck<CatalogDbContext>("sql", tags: ["db"])
                .AddCheck<MessageTypeCatalogPopulatedHealthCheck>("catalog-populated", tags: ["data"]);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(
                            "https://localhost:7206",
                            "http://localhost:5147",
                            "https://delightful-sky-05bade503.2.azurestaticapps.net")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
            var usingInMemory = string.IsNullOrEmpty(connectionString);
            if (usingInMemory)
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("CatalogDb"));
            }
            else
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
            }

            var app = builder.Build();

            app.UseCors();

            if (usingInMemory)
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                InMemorySeedData.Apply(context);
            }

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            app.MapAzureSuiteHealthChecks();

            app.MapPost("/message-types", async (RegisterMessageTypeRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new RegisterMessageTypeCommand(request.Name, request.Version, request.SchemaDefinition));
                return Results.Created($"/message-types/{dto.Name}/{dto.Version}", dto);
            });

            app.MapGet("/message-types/{name}/{version}", async (string name, string version, IMediator mediator) =>
            {
                var dto = await mediator.Send(new GetMessageTypeQuery(name, version));
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });

            app.MapGet("/message-types", async (IMediator mediator) =>
            {
                var dtos = await mediator.Send(new ListMessageTypesQuery());
                return Results.Ok(dtos);
            });

            app.MapPost("/message-types/{name}/{version}/validate", async (string name, string version, ValidateMessageRequest request, IMediator mediator) =>
            {
                var result = await mediator.Send(new ValidateMessageQuery(name, version, request.Payload));
                return Results.Ok(result);
            });

            app.MapPost("/clients", async (CreateClientRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new CreateClientCommand(request.Name));
                return Results.Created($"/clients/{dto.Id}", dto);
            });

            app.MapGet("/clients/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                var dto = await mediator.Send(new GetClientQuery(id));
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });

            app.MapGet("/clients", async (IMediator mediator) =>
            {
                var dtos = await mediator.Send(new ListClientsQuery());
                return Results.Ok(dtos);
            });

            app.MapPut("/clients/{id:guid}", async (Guid id, UpdateClientRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new UpdateClientCommand(id, request.Name));
                return Results.Ok(dto);
            });

            app.MapDelete("/clients/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                await mediator.Send(new DeleteClientCommand(id));
                return Results.NoContent();
            });

            app.MapPost("/routes", async (CreateRouteRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new CreateRouteCommand(request.ClientId, request.MessageTypeName, request.MessageTypeVersion, request.QueueNames));
                return Results.Created($"/routes/{dto.Id}", dto);
            });

            app.MapGet("/routes/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                var dto = await mediator.Send(new GetRouteQuery(id));
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });

            app.MapGet("/routes", async (IMediator mediator) =>
            {
                var dtos = await mediator.Send(new ListRoutesQuery());
                return Results.Ok(dtos);
            });

            app.MapPut("/routes/{id:guid}", async (Guid id, UpdateRouteRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new UpdateRouteCommand(id, request.ClientId, request.MessageTypeName, request.MessageTypeVersion, request.QueueNames));
                return Results.Ok(dto);
            });

            app.MapDelete("/routes/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                await mediator.Send(new DeleteRouteCommand(id));
                return Results.NoContent();
            });

            app.Run();
        }
    }
}
