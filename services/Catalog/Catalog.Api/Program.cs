using AzureSuite.Catalog.Api.Contracts;
using AzureSuite.Catalog.Api.Persistence;
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Observability;
using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
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

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(
                            "https://localhost:7204",
                            "http://localhost:5146",
                            "https://agreeable-plant-07d72b803.3.azurestaticapps.net")
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

            app.Run();
        }
    }
}
