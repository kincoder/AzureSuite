using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories;

public class MessageTypeRepositoryTests
{
    private static CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ThenGetByNameAndVersionAsync_ReturnsTheSameMessageType()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);
        var name = new MessageTypeName("pacs.008");
        var version = new MessageTypeVersion("1.0");
        var messageType = new MessageType(name, version, "{}");

        await repository.AddAsync(messageType, CancellationToken.None);
        var found = await repository.GetByNameAndVersionAsync(name, version, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(messageType.Id);
    }

    [Fact]
    public async Task GetByNameAndVersionAsync_WhenNotFound_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);

        var found = await repository.GetByNameAndVersionAsync(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRegisteredMessageTypes()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);
        await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{}"), CancellationToken.None);
        await repository.AddAsync(new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), "{}"), CancellationToken.None);

        var all = await repository.ListAsync(CancellationToken.None);

        all.Should().HaveCount(2);
        all.Select(m => m.Name.Value).Should().BeEquivalentTo("pacs.008", "camt.054");
    }
}
