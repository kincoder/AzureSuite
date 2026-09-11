using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Application.Abstractions
{
    /// <summary>
    /// Persistence contract for <see cref="MessageType"/>, defined here (Application layer) and
    /// implemented in Infrastructure — classic dependency inversion, so command/query handlers
    /// never depend on EF Core directly.
    /// </summary>
    public interface IMessageTypeRepository
    {
        Task AddAsync(MessageType messageType, CancellationToken cancellationToken);

        /// <summary>Looks up a message type by its natural key (name + version). Returns null if not registered.</summary>
        Task<MessageType?> GetByNameAndVersionAsync(MessageTypeName name, MessageTypeVersion version, CancellationToken cancellationToken);

        Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken);
    }
}
