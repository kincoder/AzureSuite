using AzureSuite.Catalog.Application.Common;
using AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Commands.UpdateRoute
{
    public class UpdateRouteHandlerTests
    {
        private readonly FakeRouteRepository _routes = new();
        private readonly FakeClientRepository _clients = new();
        private readonly FakeMessageTypeRepository _messageTypes = new();

        private UpdateRouteHandler NewHandler() => new(_routes, _clients, _messageTypes);

        private async Task<(Client Client, Route Route)> ArrangeExistingRouteAsync()
        {
            var client = new Client("Contoso Payments");
            await _clients.AddAsync(client, CancellationToken.None);
            await _messageTypes.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"), CancellationToken.None);
            await _messageTypes.AddAsync(new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), "{ \"type\": \"object\" }"), CancellationToken.None);

            var route = new Route(client.Id, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" });
            await _routes.AddAsync(route, CancellationToken.None);
            return (client, route);
        }

        [Fact]
        public async Task Handle_WithRegisteredMessageType_UpdatesRoute()
        {
            var (client, route) = await ArrangeExistingRouteAsync();

            var result = await NewHandler().Handle(
                new UpdateRouteCommand(route.Id, client.Id, "camt.054", "1.0", new[] { "queue-b" }),
                CancellationToken.None);

            result.Id.Should().Be(route.Id);
            var stored = await _routes.GetByIdAsync(route.Id, CancellationToken.None);
            stored!.MessageTypeName.Value.Should().Be("camt.054");
            stored.QueueNames.Should().Equal("queue-b");
        }

        [Fact]
        public async Task Handle_WithUnknownClient_ThrowsAndLeavesRouteUnchanged()
        {
            var (_, route) = await ArrangeExistingRouteAsync();

            var act = () => NewHandler().Handle(
                new UpdateRouteCommand(route.Id, Guid.NewGuid(), "pacs.008", "1.0", new[] { "queue-b" }),
                CancellationToken.None);

            await act.Should().ThrowAsync<ReferencedEntityNotFoundException>().WithMessage("Client *");
            (await _routes.GetByIdAsync(route.Id, CancellationToken.None))!.QueueNames.Should().Equal("queue-a");
        }

        [Fact]
        public async Task Handle_WithUnregisteredMessageType_ThrowsAndLeavesRouteUnchanged()
        {
            var (client, route) = await ArrangeExistingRouteAsync();

            var act = () => NewHandler().Handle(
                new UpdateRouteCommand(route.Id, client.Id, "pain.001", "1.0", new[] { "queue-b" }),
                CancellationToken.None);

            await act.Should().ThrowAsync<ReferencedEntityNotFoundException>().WithMessage("Message type 'pain.001'*");
            (await _routes.GetByIdAsync(route.Id, CancellationToken.None))!.MessageTypeName.Value.Should().Be("pacs.008");
        }
    }
}
