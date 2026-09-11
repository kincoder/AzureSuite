using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Domain.Entities;

public class MessageType
{
    public Guid Id { get; }
    public MessageTypeName Name { get; }
    public MessageTypeVersion Version { get; }
    public string SchemaDefinition { get; }
    public DateTime RegisteredAtUtc { get; }

    public MessageType(MessageTypeName name, MessageTypeVersion version, string schemaDefinition)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(version);

        if (string.IsNullOrWhiteSpace(schemaDefinition))
        {
            throw new ArgumentException("Schema definition is required.", nameof(schemaDefinition));
        }

        Id = Guid.NewGuid();
        Name = name;
        Version = version;
        SchemaDefinition = schemaDefinition;
        RegisteredAtUtc = DateTime.UtcNow;
    }
}
