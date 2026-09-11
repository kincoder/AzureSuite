using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;

public record GetMessageTypeQuery(string Name, string Version) : IRequest<MessageTypeDto?>;
