using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Domain.Entities
{
    /// <summary>
    /// A financial message type registered in the Catalog: the record that tells the rest of
    /// the messaging hub (Ingestion, Routing, Delivery) that a given message name/version is
    /// known, and what shape it must have.
    /// </summary>
    public class MessageType
    {
        /// <summary>Unique identifier assigned at registration time.</summary>
        public Guid Id { get; }

        public MessageTypeName Name { get; }

        public MessageTypeVersion Version { get; }

        /// <summary>
        /// The schema an inbound message of this type/version must satisfy, e.g. a JSON Schema
        /// document. Ingestion validates producer submissions against this before accepting them.
        /// </summary>
        public string SchemaDefinition { get; }

        /// <summary>UTC timestamp of when this message type was registered in the Catalog.</summary>
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
}
