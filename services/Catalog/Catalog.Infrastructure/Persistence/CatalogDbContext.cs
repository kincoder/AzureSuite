using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence
{
    /// <summary>EF Core context for the Catalog service's own database — owns the MessageTypes table only.</summary>
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

                // Value objects are stored as their plain string Value via a converter; the
                // Name/Version columns stay simple strings in the database.
                entity.Property(m => m.Name)
                    .HasConversion(name => name.Value, value => new MessageTypeName(value))
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(m => m.Version)
                    .HasConversion(version => version.Value, value => new MessageTypeVersion(value))
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(m => new { m.Name, m.Version }).IsUnique();
                entity.Property(m => m.SchemaDefinition).IsRequired();
            });
        }
    }
}
