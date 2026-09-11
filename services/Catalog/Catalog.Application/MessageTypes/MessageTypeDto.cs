namespace AzureSuite.Catalog.Application.MessageTypes
{
    /// <summary>
    /// Read-facing shape of a registered message type, returned by commands/queries and
    /// serialized over the API. Keeps the Domain entity (<see cref="AzureSuite.Catalog.Domain.Entities.MessageType"/>)
    /// out of the wire contract.
    /// </summary>
    /// <param name="Id">Unique identifier assigned at registration time.</param>
    /// <param name="Name">Message type name, e.g. "pacs.008".</param>
    /// <param name="Version">Schema version, e.g. "1.0".</param>
    /// <param name="SchemaDefinition">The schema inbound messages of this type/version must satisfy.</param>
    /// <param name="RegisteredAtUtc">UTC timestamp of registration.</param>
    public record MessageTypeDto(Guid Id, string Name, string Version, string SchemaDefinition, DateTime RegisteredAtUtc);
}
