using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;

public record ListMessageTypesQuery : IRequest<IReadOnlyList<MessageTypeDto>>;
