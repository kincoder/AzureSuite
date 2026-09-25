using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    /// <summary>Checks only whether Payload is structurally valid for the given MessageType/
    /// Version's registered schema. Knows nothing about clients or routing.</summary>
    public record ValidateMessageQuery(string MessageTypeName, string MessageTypeVersion, string Payload) : IRequest<ValidationResultDto>;
}
