using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes
{
    /// <summary>Lists every message type currently registered in the Catalog.</summary>
    public record ListMessageTypesQuery : IRequest<IReadOnlyList<MessageTypeDto>>;
}
