using AzureSuite.Application.Abstractions;
using AzureSuite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Infrastructure.Persistence.Repositories;

public class Pacs008MessageRepository(AppDbContext dbContext) : IPacs008MessageRepository
{
    public async Task<IReadOnlyList<Pacs008Message>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Pacs008Messages.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Pacs008Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Pacs008Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task AddAsync(Pacs008Message message, CancellationToken cancellationToken = default)
    {
        dbContext.Pacs008Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
