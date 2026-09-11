using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.GetMessageType
{
    public class GetMessageTypeHandlerTests
    {
        [Fact]
        public async Task Handle_WhenMessageTypeExists_ReturnsDto()
        {
            var repository = new FakeMessageTypeRepository();
            await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{}"), CancellationToken.None);
            var handler = new GetMessageTypeHandler(repository);

            var result = await handler.Handle(new GetMessageTypeQuery("pacs.008", "1.0"), CancellationToken.None);

            result.Should().NotBeNull();
            result!.Name.Should().Be("pacs.008");
        }

        [Fact]
        public async Task Handle_WhenMessageTypeDoesNotExist_ReturnsNull()
        {
            var repository = new FakeMessageTypeRepository();
            var handler = new GetMessageTypeHandler(repository);

            var result = await handler.Handle(new GetMessageTypeQuery("camt.054", "1.0"), CancellationToken.None);

            result.Should().BeNull();
        }
    }
}
