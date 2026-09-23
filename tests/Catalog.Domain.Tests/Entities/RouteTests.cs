using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities
{
    public class RouteTests
    {
        [Fact]
        public void Constructor_WithQueueNames_SetsProperties()
        {
            var clientId = Guid.NewGuid();
            var route = new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" });

            route.Id.Should().NotBeEmpty();
            route.ClientId.Should().Be(clientId);
            route.MessageTypeName.Value.Should().Be("pacs.008");
            route.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public void Constructor_WithNoQueueNames_ThrowsArgumentException()
        {
            var act = () => new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Array.Empty<string>());

            act.Should().Throw<ArgumentException>();
        }
    }
}
