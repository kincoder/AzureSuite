using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly CatalogDbContext _context;

        public ClientRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Client client, CancellationToken cancellationToken)
        {
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.Clients.ToListAsync(cancellationToken);
        }

        public async Task UpdateAsync(Guid id, string name, CancellationToken cancellationToken)
        {
            var existing = await _context.Clients.FirstAsync(c => c.Id == id, cancellationToken);
            _context.Entry(existing).CurrentValues.SetValues(new { existing.Id, Name = name, existing.RegisteredAtUtc });
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (existing is not null)
            {
                _context.Clients.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
