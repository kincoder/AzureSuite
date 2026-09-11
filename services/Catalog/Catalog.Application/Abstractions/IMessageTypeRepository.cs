using AzureSuite.Catalog.Domain.Entities;

namespace AzureSuite.Catalog.Application.Abstractions;

public interface IMessageTypeRepository
{
    Task AddAsync(MessageType messageType, CancellationToken cancellationToken);

    Task<MessageType?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken);
}
