using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;

public record RegisterMessageTypeCommand(string Name, string Version, string SchemaDefinition) : IRequest<MessageTypeDto>;
