using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;

public class GetMessageTypeHandler : IRequestHandler<GetMessageTypeQuery, MessageTypeDto?>
{
    private readonly IMessageTypeRepository _repository;

    public GetMessageTypeHandler(IMessageTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<MessageTypeDto?> Handle(GetMessageTypeQuery request, CancellationToken cancellationToken)
    {
        var name = new MessageTypeName(request.Name);
        var version = new MessageTypeVersion(request.Version);

        var messageType = await _repository.GetByNameAndVersionAsync(name, version, cancellationToken);
        if (messageType is null)
        {
            return null;
        }

        return new MessageTypeDto(messageType.Id, messageType.Name.Value, messageType.Version.Value, messageType.SchemaDefinition, messageType.RegisteredAtUtc);
    }
}
