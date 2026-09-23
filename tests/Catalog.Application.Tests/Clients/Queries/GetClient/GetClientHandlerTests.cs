using AzureSuite.Catalog.Application.Clients.Queries.GetClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Queries.GetClient
{
    public class GetClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_ReturnsDto()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new GetClientHandler(repository);

            var result = await handler.Handle(new GetClientQuery(client.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Acme Corp");
        }

        [Fact]
        public async Task Handle_WithUnknownId_ReturnsNull()
        {
            var handler = new GetClientHandler(new FakeClientRepository());

            var result = await handler.Handle(new GetClientQuery(Guid.NewGuid()), CancellationToken.None);

            result.Should().BeNull();
        }
    }
}
