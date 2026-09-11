using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Api
{
    /// <summary>Entry point and endpoint registration for the Catalog service's HTTP API.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterMessageTypeCommand).Assembly));
            builder.Services.AddScoped<IMessageTypeRepository, MessageTypeRepository>();

            var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
            if (string.IsNullOrEmpty(connectionString))
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("CatalogDb"));
            }
            else
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
            }

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
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

    /// <summary>Request body for registering a new message type via <c>POST /message-types</c>.</summary>
    public record RegisterMessageTypeRequest(string Name, string Version, string SchemaDefinition);
}
