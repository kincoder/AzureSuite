using AzureSuite.Catalog.Application.Routes.Queries.GetRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.GetRoute
{
    public class GetRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_ReturnsDto()
        {
            var repository = new FakeRouteRepository();
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" });
            await repository.AddAsync(route, CancellationToken.None);
            var handler = new GetRouteHandler(repository);

            var result = await handler.Handle(new GetRouteQuery(route.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.QueueNames.Should().Equal("queue-a");
        }
    }
}
