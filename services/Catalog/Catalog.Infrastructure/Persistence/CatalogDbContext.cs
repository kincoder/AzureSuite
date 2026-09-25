using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence
{
    /// <summary>EF Core context for the Catalog service's own database: MessageTypes, Clients and Routes.</summary>
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        public DbSet<MessageType> MessageTypes => Set<MessageType>();

        public DbSet<Client> Clients => Set<Client>();

        public DbSet<Route> Routes => Set<Route>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MessageType>(entity =>
            {
                entity.HasKey(m => m.Id);

                // Id is assigned in the constructor (Guid.NewGuid()), not by the database.
                entity.Property(m => m.Id).ValueGeneratedNever();

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
                entity.Property(m => m.RegisteredAtUtc).IsRequired();
            });

            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedNever();
                entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
                entity.Property(c => c.RegisteredAtUtc).IsRequired();
            });

            modelBuilder.Entity<Route>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedNever();
                entity.Property(r => r.ClientId).IsRequired();

                entity.Property(r => r.MessageTypeName)
                    .HasConversion(name => name.Value, value => new MessageTypeName(value))
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(r => r.MessageTypeVersion)
                    .HasConversion(version => version.Value, value => new MessageTypeVersion(value))
                    .IsRequired()
                    .HasMaxLength(20);

                // Primitive collection of strings -> JSON column (EF Core 8+ default for
                // relational providers). Simpler than a normalized join table at this scale
                // (1-3 queue names per route).
                entity.PrimitiveCollection(r => r.QueueNames).IsRequired();

                entity.HasIndex(r => new { r.ClientId, r.MessageTypeName, r.MessageTypeVersion });

                // Deleting a client revokes its authorizations, so its routes go with it.
                entity.HasOne<Client>()
                    .WithMany()
                    .HasForeignKey(r => r.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Routes reference a message type by its natural key (Name, Version) rather than
                // its Id, so route lookup stays a single-table query. A message type that is
                // still routed cannot be deleted.
                entity.HasOne<MessageType>()
                    .WithMany()
                    .HasForeignKey(r => new { r.MessageTypeName, r.MessageTypeVersion })
                    .HasPrincipalKey(m => new { m.Name, m.Version })
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
