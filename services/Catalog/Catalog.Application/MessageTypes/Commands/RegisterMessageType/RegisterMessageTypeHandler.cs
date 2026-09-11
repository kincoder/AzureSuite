using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType
{
    public class RegisterMessageTypeHandler : IRequestHandler<RegisterMessageTypeCommand, MessageTypeDto>
    {
        private readonly IMessageTypeRepository _repository;

        public RegisterMessageTypeHandler(IMessageTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<MessageTypeDto> Handle(RegisterMessageTypeCommand request, CancellationToken cancellationToken)
        {
            var name = new MessageTypeName(request.Name);
            var version = new MessageTypeVersion(request.Version);

            var existing = await _repository.GetByNameAndVersionAsync(name, version, cancellationToken);
            if (existing is not null)
            {
                throw new InvalidOperationException(
                    $"Message type '{name}' version '{version}' is already registered.");
            }

            var messageType = new MessageType(name, version, request.SchemaDefinition);
            await _repository.AddAsync(messageType, cancellationToken);

            return new MessageTypeDto(messageType.Id, messageType.Name.Value, messageType.Version.Value, messageType.SchemaDefinition, messageType.RegisteredAtUtc);
        }
    }
}
