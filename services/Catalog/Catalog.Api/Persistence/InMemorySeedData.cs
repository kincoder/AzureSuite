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
        // Same schemas as scripts/seed-routing-test-data.sql, which seeds a real SQL database.
        private const string Pacs008Schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "pacs.008 FI to FI Customer Credit Transfer",
              "type": "object",
              "required": ["messageId", "creationDateTime", "amount", "debtor", "creditor"],
              "properties": {
                "messageId": { "type": "string", "maxLength": 35 },
                "creationDateTime": { "type": "string", "format": "date-time" },
                "amount": {
                  "type": "object",
                  "required": ["value", "currency"],
                  "properties": {
                    "value": { "type": "number", "exclusiveMinimum": 0 },
                    "currency": { "type": "string", "pattern": "^[A-Z]{3}$" }
                  }
                },
                "debtor": { "$ref": "#/$defs/party" },
                "creditor": { "$ref": "#/$defs/party" }
              },
              "$defs": {
                "party": {
                  "type": "object",
                  "required": ["name", "iban"],
                  "properties": {
                    "name": { "type": "string", "maxLength": 140 },
                    "iban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" }
                  }
                }
              }
            }
            """;

        private const string Pacs002Schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "pacs.002 Payment Status Report",
              "type": "object",
              "required": ["messageId", "originalMessageId", "status"],
              "properties": {
                "messageId": { "type": "string", "maxLength": 35 },
                "originalMessageId": { "type": "string", "maxLength": 35 },
                "status": { "enum": ["ACCP", "ACSC", "PDNG", "RJCT"] },
                "reasonCode": { "type": "string", "maxLength": 4 }
              }
            }
            """;

        private const string Pain001Schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "pain.001 Customer Credit Transfer Initiation",
              "type": "object",
              "required": ["messageId", "initiatingParty", "payments"],
              "properties": {
                "messageId": { "type": "string", "maxLength": 35 },
                "initiatingParty": { "type": "string", "maxLength": 140 },
                "payments": {
                  "type": "array",
                  "minItems": 1,
                  "items": {
                    "type": "object",
                    "required": ["endToEndId", "amount", "currency", "creditorIban"],
                    "properties": {
                      "endToEndId": { "type": "string", "maxLength": 35 },
                      "amount": { "type": "number", "exclusiveMinimum": 0 },
                      "currency": { "type": "string", "pattern": "^[A-Z]{3}$" },
                      "creditorIban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" }
                    }
                  }
                }
              }
            }
            """;

        private const string Camt054Schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "camt.054 Bank to Customer Debit/Credit Notification",
              "type": "object",
              "required": ["messageId", "accountIban", "entries"],
              "properties": {
                "messageId": { "type": "string", "maxLength": 35 },
                "accountIban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" },
                "entries": {
                  "type": "array",
                  "minItems": 1,
                  "items": {
                    "type": "object",
                    "required": ["amount", "currency", "creditDebitIndicator", "bookingDate"],
                    "properties": {
                      "amount": { "type": "number", "exclusiveMinimum": 0 },
                      "currency": { "type": "string", "pattern": "^[A-Z]{3}$" },
                      "creditDebitIndicator": { "enum": ["CRDT", "DBIT"] },
                      "bookingDate": { "type": "string", "format": "date" }
                    }
                  }
                }
              }
            }
            """;

        public static void Apply(CatalogDbContext context)
        {
            if (context.MessageTypes.Any())
            {
                return;
            }

            context.MessageTypes.AddRange(
                new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Pacs008Schema),
                new MessageType(new MessageTypeName("pacs.002"), new MessageTypeVersion("1.0"), Pacs002Schema),
                new MessageType(new MessageTypeName("pain.001"), new MessageTypeVersion("1.0"), Pain001Schema),
                new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), Camt054Schema));

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
