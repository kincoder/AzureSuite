using AzureSuite.Domain.Entities;

namespace AzureSuite.Application.Abstractions;

/// <summary>
/// Persistence contract for PACS.008 messages. Defined here (Application layer) and
/// implemented in Infrastructure, so Application/Domain stay free of any EF Core or
/// database-specific dependency - the classic Dependency Inversion piece of DDD/Clean
/// Architecture.
/// </summary>
public interface IPacs008MessageRepository
{
    Task<IReadOnlyList<Pacs008Message>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Pacs008Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Pacs008Message message, CancellationToken cancellationToken = default);
}
