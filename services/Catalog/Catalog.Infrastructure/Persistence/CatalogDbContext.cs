using AzureSuite.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<MessageType> MessageTypes => Set<MessageType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageType>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.Name, m.Version }).IsUnique();
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
            entity.Property(m => m.Version).IsRequired().HasMaxLength(20);
            entity.Property(m => m.SchemaDefinition).IsRequired();
        });
    }
}
