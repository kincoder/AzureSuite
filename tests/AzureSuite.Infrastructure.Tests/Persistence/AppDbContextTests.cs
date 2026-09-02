using AzureSuite.Domain.Entities;
using AzureSuite.Domain.ValueObjects;
using AzureSuite.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Infrastructure.Tests.Persistence;

public class AppDbContextTests
{
    // Each test gets its own uniquely-named in-memory database so tests can run
    // in parallel without sharing state.
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Pacs008Message CreateMessage() => new()
    {
        MessageId = "MSG-0001",
        EndToEndId = "E2E-0001",
        Amount = 100.50m,
        Currency = "EUR",
        Debtor = new PartyAccount { Name = "Alice", Iban = "DE89370400440532013000", BicCode = "COBADEFFXXX" },
        Creditor = new PartyAccount { Name = "Bob", Iban = "FR1420041010050500013M02606", BicCode = "PSSTFRPPXXX" }
    };

    [Fact]
    public void MessageId_HasUniqueIndex()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Pacs008Message))!;
        var index = entityType.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Pacs008Message.MessageId));

        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task Messages_CanBeAddedAndRetrieved_WithOwnedDebtorAndCreditor()
    {
        using var context = CreateContext();
        var message = CreateMessage();

        context.Pacs008Messages.Add(message);
        await context.SaveChangesAsync();

        var retrieved = await context.Pacs008Messages.FindAsync(message.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Debtor.Name.Should().Be("Alice");
        retrieved.Creditor.Name.Should().Be("Bob");
    }
}
