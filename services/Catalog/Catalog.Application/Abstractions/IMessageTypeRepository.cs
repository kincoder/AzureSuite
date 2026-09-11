using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Application.Abstractions;

public interface IMessageTypeRepository
{
    Task AddAsync(MessageType messageType, CancellationToken cancellationToken);

    Task<MessageType?> GetByNameAndVersionAsync(MessageTypeName name, MessageTypeVersion version, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken);
}
