using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Application.Abstractions
{
    public interface IRouteRepository
    {
        Task AddAsync(Route route, CancellationToken cancellationToken);
        Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<Route>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Used by the route-lookup query: every Route authorizing this client to
        /// send this message type/version.</summary>
        Task<IReadOnlyList<Route>> ListByClientAndTypeAsync(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, CancellationToken cancellationToken);

        /// <summary>Updates the Route identified by id. Takes id and the new field values
        /// directly rather than a reconstructed Route -- a freshly-constructed Route always
        /// mints its own new Id, which would never match the row being updated.</summary>
        Task UpdateAsync(Guid id, Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IReadOnlyList<string> queueNames, CancellationToken cancellationToken);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
