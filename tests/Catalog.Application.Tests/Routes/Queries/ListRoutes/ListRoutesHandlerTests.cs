using AzureSuite.Catalog.Application.Routes.Queries.ListRoutes;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.ListRoutes
{
    public class ListRoutesHandlerTests
    {
        [Fact]
        public async Task Handle_ReturnsAllRoutes()
        {
            var repository = new FakeRouteRepository();
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "queue-b", "queue-c" }), CancellationToken.None);
            var handler = new ListRoutesHandler(repository);

            var result = await handler.Handle(new ListRoutesQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }
    }
}
