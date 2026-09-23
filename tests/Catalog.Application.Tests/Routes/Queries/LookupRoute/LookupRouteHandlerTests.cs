using AzureSuite.Catalog.Application.Routes.Queries.LookupRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.LookupRoute
{
    public class LookupRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithAuthorizedClientAndType_ReturnsQueueNames()
        {
            var repository = new FakeRouteRepository();
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" }), CancellationToken.None);
            var handler = new LookupRouteHandler(repository);

            var result = await handler.Handle(new LookupRouteQuery(clientId, "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public async Task Handle_WithNoMatchingRoute_ReturnsEmptyQueueNames()
        {
            var handler = new LookupRouteHandler(new FakeRouteRepository());

            var result = await handler.Handle(new LookupRouteQuery(Guid.NewGuid(), "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithMultipleRoutesForSamePair_MergesAllQueueNames()
        {
            var repository = new FakeRouteRepository();
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-b" }), CancellationToken.None);
            var handler = new LookupRouteHandler(repository);

            var result = await handler.Handle(new LookupRouteQuery(clientId, "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().BeEquivalentTo(new[] { "queue-a", "queue-b" });
        }
    }
}
