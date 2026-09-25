using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories
{
    public class RouteRepository : IRouteRepository
    {
        private readonly CatalogDbContext _context;

        public RouteRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Route route, CancellationToken cancellationToken)
        {
            _context.Routes.Add(route);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Route>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.Routes.ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Route>> ListByClientAndTypeAsync(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, CancellationToken cancellationToken)
        {
            return await _context.Routes
                .Where(r => r.ClientId == clientId && r.MessageTypeName == messageTypeName && r.MessageTypeVersion == messageTypeVersion)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateAsync(Guid id, Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IReadOnlyList<string> queueNames, CancellationToken cancellationToken)
        {
            var existing = await _context.Routes.FirstAsync(r => r.Id == id, cancellationToken);
            var entry = _context.Entry(existing);
            entry.Property(r => r.ClientId).CurrentValue = clientId;
            entry.Property(r => r.MessageTypeName).CurrentValue = messageTypeName;
            entry.Property(r => r.MessageTypeVersion).CurrentValue = messageTypeVersion;
            entry.Property(r => r.QueueNames).CurrentValue = queueNames;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existing is not null)
            {
                _context.Routes.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
