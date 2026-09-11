namespace AzureSuite.Catalog.Application.MessageTypes;

public record MessageTypeDto(Guid Id, string Name, string Version, string SchemaDefinition, DateTime RegisteredAtUtc);
