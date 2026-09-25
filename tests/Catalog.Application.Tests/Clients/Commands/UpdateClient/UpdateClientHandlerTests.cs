using AzureSuite.Catalog.Application.Clients.Commands.UpdateClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.UpdateClient
{
    public class UpdateClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_UpdatesName()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new UpdateClientHandler(repository);

            var result = await handler.Handle(new UpdateClientCommand(client.Id, "Acme Corporation"), CancellationToken.None);

            result.Name.Should().Be("Acme Corporation");
        }

        [Fact]
        public async Task Handle_WithUnknownId_ThrowsInvalidOperationException()
        {
            var handler = new UpdateClientHandler(new FakeClientRepository());

            var act = () => handler.Handle(new UpdateClientCommand(Guid.NewGuid(), "X"), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
