using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;

public class RegisterMessageTypeHandler : IRequestHandler<RegisterMessageTypeCommand, MessageTypeDto>
{
    private readonly IMessageTypeRepository _repository;

    public RegisterMessageTypeHandler(IMessageTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<MessageTypeDto> Handle(RegisterMessageTypeCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByNameAndVersionAsync(request.Name, request.Version, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Message type '{request.Name}' version '{request.Version}' is already registered.");
        }

        var messageType = new MessageType(request.Name, request.Version, request.SchemaDefinition);
        await _repository.AddAsync(messageType, cancellationToken);

        return new MessageTypeDto(messageType.Id, messageType.Name, messageType.Version, messageType.SchemaDefinition, messageType.RegisteredAtUtc);
    }
}
