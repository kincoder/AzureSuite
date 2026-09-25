using AzureSuite.Catalog.Application.Common;
using AzureSuite.Catalog.Application.Routes.Commands.CreateRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Commands.CreateRoute
{
    public class CreateRouteHandlerTests
    {
        private readonly FakeRouteRepository _routes = new();
        private readonly FakeClientRepository _clients = new();
        private readonly FakeMessageTypeRepository _messageTypes = new();

        private CreateRouteHandler NewHandler() => new(_routes, _clients, _messageTypes);

        private async Task<Client> AddClientAsync()
        {
            var client = new Client("Contoso Payments");
            await _clients.AddAsync(client, CancellationToken.None);
            return client;
        }

        private Task AddMessageTypeAsync(string name, string version) =>
            _messageTypes.AddAsync(new MessageType(new MessageTypeName(name), new MessageTypeVersion(version), "{ \"type\": \"object\" }"), CancellationToken.None);

        [Fact]
        public async Task Handle_WithValidRequest_CreatesAndReturnsDto()
        {
            var client = await AddClientAsync();
            await AddMessageTypeAsync("pacs.008", "1.0");

            var result = await NewHandler().Handle(
                new CreateRouteCommand(client.Id, "pacs.008", "1.0", new[] { "queue-a" }),
                CancellationToken.None);

            result.ClientId.Should().Be(client.Id);
            result.QueueNames.Should().Equal("queue-a");
        }

        [Fact]
        public async Task Handle_WithUnknownClient_ThrowsAndDoesNotAddRoute()
        {
            await AddMessageTypeAsync("pacs.008", "1.0");

            var act = () => NewHandler().Handle(
                new CreateRouteCommand(Guid.NewGuid(), "pacs.008", "1.0", new[] { "queue-a" }),
                CancellationToken.None);

            await act.Should().ThrowAsync<ReferencedEntityNotFoundException>().WithMessage("Client *");
            (await _routes.ListAsync(CancellationToken.None)).Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithUnregisteredMessageTypeVersion_ThrowsAndDoesNotAddRoute()
        {
            var client = await AddClientAsync();
            await AddMessageTypeAsync("pacs.008", "1.0");

            var act = () => NewHandler().Handle(
                new CreateRouteCommand(client.Id, "pacs.008", "2.0", new[] { "queue-a" }),
                CancellationToken.None);

            await act.Should().ThrowAsync<ReferencedEntityNotFoundException>().WithMessage("Message type 'pacs.008' version '2.0'*");
            (await _routes.ListAsync(CancellationToken.None)).Should().BeEmpty();
        }
    }
}
