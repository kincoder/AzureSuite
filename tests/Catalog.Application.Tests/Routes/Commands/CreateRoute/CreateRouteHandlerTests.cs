using AzureSuite.Catalog.Application.Routes.Commands.CreateRoute;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Commands.CreateRoute
{
    public class CreateRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidRequest_CreatesAndReturnsDto()
        {
            var repository = new FakeRouteRepository();
            var handler = new CreateRouteHandler(repository);
            var clientId = Guid.NewGuid();

            var result = await handler.Handle(
                new CreateRouteCommand(clientId, "pacs.008", "1.0", new[] { "queue-a" }),
                CancellationToken.None);

            result.ClientId.Should().Be(clientId);
            result.QueueNames.Should().Equal("queue-a");
        }
    }
}
