using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories
{
    /// <summary>EF Core-backed implementation of <see cref="IMessageTypeRepository"/>.</summary>
    public class MessageTypeRepository : IMessageTypeRepository
    {
        private readonly CatalogDbContext _context;

        public MessageTypeRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MessageType messageType, CancellationToken cancellationToken)
        {
            _context.MessageTypes.Add(messageType);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<MessageType?> GetByNameAndVersionAsync(MessageTypeName name, MessageTypeVersion version, CancellationToken cancellationToken)
        {
            return _context.MessageTypes
                .FirstOrDefaultAsync(m => m.Name == name && m.Version == version, cancellationToken);
        }

        public async Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.MessageTypes.ToListAsync(cancellationToken);
        }
    }
}
