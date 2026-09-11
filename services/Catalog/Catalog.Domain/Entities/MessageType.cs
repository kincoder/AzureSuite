namespace AzureSuite.Catalog.Domain.Entities;

public class MessageType
{
    public Guid Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string SchemaDefinition { get; }
    public DateTime RegisteredAtUtc { get; }

    public MessageType(string name, string version, string schemaDefinition)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version is required.", nameof(version));
        }

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
