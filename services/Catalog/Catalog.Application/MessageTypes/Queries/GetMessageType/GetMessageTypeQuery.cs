using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType
{
    /// <summary>Looks up a single registered message type by its natural key (name + version).</summary>
    /// <param name="Name">Message type name, e.g. "pacs.008".</param>
    /// <param name="Version">Schema version, e.g. "1.0".</param>
    public record GetMessageTypeQuery(string Name, string Version) : IRequest<MessageTypeDto?>;
}
