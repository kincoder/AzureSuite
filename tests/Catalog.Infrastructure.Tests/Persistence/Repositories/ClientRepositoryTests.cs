using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories
{
    public class ClientRepositoryTests
    {
        private static CatalogDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_ReturnsSameClient()
        {
            await using var context = NewContext();
            var repository = new ClientRepository(context);
            var client = new Client("Acme Corp");

            await repository.AddAsync(client, CancellationToken.None);
            var found = await repository.GetByIdAsync(client.Id, CancellationToken.None);

            found.Should().NotBeNull();
            found!.Name.Should().Be("Acme Corp");
        }

        [Fact]
        public async Task UpdateAsync_UpdatesNameButPreservesIdAndRegisteredAtUtc()
        {
            await using var context = NewContext();
            var repository = new ClientRepository(context);
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);

            await repository.UpdateAsync(client.Id, "Acme Corporation", CancellationToken.None);
            var found = await repository.GetByIdAsync(client.Id, CancellationToken.None);

            found.Should().NotBeNull();
            found!.Id.Should().Be(client.Id);
            found.Name.Should().Be("Acme Corporation");
            found.RegisteredAtUtc.Should().Be(client.RegisteredAtUtc);
        }

        [Fact]
        public async Task DeleteAsync_RemovesClient()
        {
            await using var context = NewContext();
            var repository = new ClientRepository(context);
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);

            await repository.DeleteAsync(client.Id, CancellationToken.None);

            (await repository.GetByIdAsync(client.Id, CancellationToken.None)).Should().BeNull();
        }
    }
}
