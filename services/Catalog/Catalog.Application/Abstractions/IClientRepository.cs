using AzureSuite.Catalog.Domain.Entities;

namespace AzureSuite.Catalog.Application.Abstractions
{
    public interface IClientRepository
    {
        Task AddAsync(Client client, CancellationToken cancellationToken);
        Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Updates the Name of the Client identified by id. Takes id and the new
        /// value directly rather than a reconstructed Client — a freshly-constructed Client
        /// always mints its own new Id, which would never match the row being updated.</summary>
        Task UpdateAsync(Guid id, string name, CancellationToken cancellationToken);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
