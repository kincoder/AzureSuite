using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;
// Catalog.Api is a Web SDK project with Microsoft.AspNetCore.Routing in its global usings,
// which collides with our own domain Route entity -- alias to disambiguate.
using CatalogRoute = AzureSuite.Catalog.Domain.Entities.Route;

namespace AzureSuite.Catalog.Api.Persistence
{
    /// <summary>Sample message types inserted into the InMemory database on startup, so local
    /// frontend development never requires Azure SQL to be running.</summary>
    public static class InMemorySeedData
    {
        public static void Apply(CatalogDbContext context)
        {
            if (context.MessageTypes.Any())
            {
                return;
            }

            context.MessageTypes.AddRange(
                new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"),
                new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"));

            context.SaveChanges();

            if (!context.Clients.Any())
            {
                var clientA = new Client("Contoso Payments");
                var clientB = new Client("Fabrikam Treasury");
                context.Clients.AddRange(clientA, clientB);
                context.SaveChanges();

                context.Routes.AddRange(
                    new CatalogRoute(clientA.Id, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "out-settlements" }),
                    new CatalogRoute(clientB.Id, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "out-settlements" }),
                    new CatalogRoute(clientB.Id, new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "out-notifications" }),
                    new CatalogRoute(clientB.Id, new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "out-audit" }));
                context.SaveChanges();
            }
        }
    }
}
