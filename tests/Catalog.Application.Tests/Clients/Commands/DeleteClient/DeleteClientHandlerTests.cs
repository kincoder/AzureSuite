using AzureSuite.Catalog.Application.Clients.Commands.DeleteClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.DeleteClient
{
    public class DeleteClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_RemovesClient()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new DeleteClientHandler(repository);

            await handler.Handle(new DeleteClientCommand(client.Id), CancellationToken.None);

            (await repository.GetByIdAsync(client.Id, CancellationToken.None)).Should().BeNull();
        }
    }
}
