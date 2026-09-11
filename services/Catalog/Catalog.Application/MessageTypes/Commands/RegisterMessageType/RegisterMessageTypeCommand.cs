using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType
{
    /// <summary>Registers a new message type/version in the Catalog.</summary>
    /// <param name="Name">Message type name, e.g. "pacs.008".</param>
    /// <param name="Version">Schema version, e.g. "1.0".</param>
    /// <param name="SchemaDefinition">The schema inbound messages of this type/version must satisfy.</param>
    public record RegisterMessageTypeCommand(string Name, string Version, string SchemaDefinition) : IRequest<MessageTypeDto>;
}
