using AzureSuite.Domain.Entities;
using AzureSuite.Domain.ValueObjects;
using AzureSuite.Infrastructure.Persistence;
using AzureSuite.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Infrastructure.Tests.Persistence.Repositories;

public class Pacs008MessageRepositoryTests
{
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
    public async Task AddAsync_PersistsMessage_RetrievableByGetAllAsync()
    {
        using var context = CreateContext();
        var repository = new Pacs008MessageRepository(context);

        await repository.AddAsync(CreateMessage());
        var all = await repository.GetAllAsync();

        all.Should().ContainSingle(m => m.MessageId == "MSG-0001");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMessageDoesNotExist()
    {
        using var context = CreateContext();
        var repository = new Pacs008MessageRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMessage_WhenItExists()
    {
        using var context = CreateContext();
        var repository = new Pacs008MessageRepository(context);
        var message = CreateMessage();
        await repository.AddAsync(message);

        var result = await repository.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.MessageId.Should().Be("MSG-0001");
    }
}
