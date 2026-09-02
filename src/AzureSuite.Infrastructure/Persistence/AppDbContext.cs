using AzureSuite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Pacs008Message> Pacs008Messages => Set<Pacs008Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pacs008Message>(entity =>
        {
            entity.HasIndex(m => m.MessageId).IsUnique();
            entity.Property(m => m.MessageId).HasMaxLength(35).IsRequired();
            entity.Property(m => m.EndToEndId).HasMaxLength(35).IsRequired();
            entity.Property(m => m.Currency).HasMaxLength(3).IsRequired();
            entity.Property(m => m.Amount).HasColumnType("decimal(18,2)");

            // Debtor/Creditor are value objects with no identity of their own,
            // stored as columns on the Pacs008Messages table rather than a separate table.
            entity.OwnsOne(m => m.Debtor, debtor =>
            {
                debtor.Property(p => p.Name).HasColumnName("DebtorName").HasMaxLength(140).IsRequired();
                debtor.Property(p => p.Iban).HasColumnName("DebtorIban").HasMaxLength(34).IsRequired();
                debtor.Property(p => p.BicCode).HasColumnName("DebtorBic").HasMaxLength(11).IsRequired();
            });

            entity.OwnsOne(m => m.Creditor, creditor =>
            {
                creditor.Property(p => p.Name).HasColumnName("CreditorName").HasMaxLength(140).IsRequired();
                creditor.Property(p => p.Iban).HasColumnName("CreditorIban").HasMaxLength(34).IsRequired();
                creditor.Property(p => p.BicCode).HasColumnName("CreditorBic").HasMaxLength(11).IsRequired();
            });
        });
    }
}
