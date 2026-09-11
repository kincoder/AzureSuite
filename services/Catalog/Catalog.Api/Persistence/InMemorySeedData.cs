using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;

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
        }
    }
}
