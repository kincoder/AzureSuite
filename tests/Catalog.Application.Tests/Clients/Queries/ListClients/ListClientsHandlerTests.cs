using AzureSuite.Catalog.Application.Clients.Queries.ListClients;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Queries.ListClients
{
    public class ListClientsHandlerTests
    {
        [Fact]
        public async Task Handle_ReturnsAllClients()
        {
            var repository = new FakeClientRepository();
            await repository.AddAsync(new Client("Acme Corp"), CancellationToken.None);
            await repository.AddAsync(new Client("Globex"), CancellationToken.None);
            var handler = new ListClientsHandler(repository);

            var result = await handler.Handle(new ListClientsQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }
    }
}
