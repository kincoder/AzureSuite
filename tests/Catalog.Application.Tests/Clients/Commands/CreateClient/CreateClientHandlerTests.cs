using AzureSuite.Catalog.Application.Clients.Commands.CreateClient;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.CreateClient
{
    public class CreateClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidName_CreatesAndReturnsDto()
        {
            var repository = new FakeClientRepository();
            var handler = new CreateClientHandler(repository);

            var result = await handler.Handle(new CreateClientCommand("Acme Corp"), CancellationToken.None);

            result.Name.Should().Be("Acme Corp");
            result.Id.Should().NotBeEmpty();
            (await repository.GetByIdAsync(result.Id, CancellationToken.None)).Should().NotBeNull();
        }
    }
}
