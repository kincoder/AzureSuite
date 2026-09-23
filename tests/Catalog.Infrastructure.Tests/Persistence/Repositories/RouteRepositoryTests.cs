using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories
{
    public class RouteRepositoryTests
    {
        private static CatalogDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_PreservesQueueNames()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" });

            await repository.AddAsync(route, CancellationToken.None);
            var found = await repository.GetByIdAsync(route.Id, CancellationToken.None);

            found!.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public async Task ListByClientAndTypeAsync_ReturnsOnlyMatchingRoutes()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-b" }), CancellationToken.None);

            var result = await repository.ListByClientAndTypeAsync(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), CancellationToken.None);

            result.Should().ContainSingle().Which.QueueNames.Should().Equal("queue-a");
        }

        [Fact]
        public async Task UpdateAsync_UpdatesFieldsButPreservesId()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" });
            await repository.AddAsync(route, CancellationToken.None);
            var newClientId = Guid.NewGuid();

            await repository.UpdateAsync(route.Id, newClientId, new MessageTypeName("camt.054"), new MessageTypeVersion("2.0"), new[] { "queue-z" }, CancellationToken.None);
            var found = await repository.GetByIdAsync(route.Id, CancellationToken.None);

            found!.Id.Should().Be(route.Id);
            found.ClientId.Should().Be(newClientId);
            found.MessageTypeName.Value.Should().Be("camt.054");
            found.QueueNames.Should().Equal("queue-z");
        }

        [Fact]
        public async Task DeleteAsync_RemovesRoute()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" });
            await repository.AddAsync(route, CancellationToken.None);

            await repository.DeleteAsync(route.Id, CancellationToken.None);

            (await repository.GetByIdAsync(route.Id, CancellationToken.None)).Should().BeNull();
        }
    }
}
