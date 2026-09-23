using AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.ValidateMessage
{
    public class ValidateMessageHandlerTests
    {
        private const string Schema = """
        {
          "type": "object",
          "properties": { "amount": { "type": "number" } },
          "required": ["amount"]
        }
        """;

        [Fact]
        public async Task Handle_WithConformingPayload_ReturnsValid()
        {
            var repository = new FakeMessageTypeRepository();
            await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Schema), CancellationToken.None);
            var handler = new ValidateMessageHandler(repository);

            var result = await handler.Handle(new ValidateMessageQuery("pacs.008", "1.0", """{ "amount": 100 }"""), CancellationToken.None);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithNonConformingPayload_ReturnsInvalidWithErrors()
        {
            var repository = new FakeMessageTypeRepository();
            await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Schema), CancellationToken.None);
            var handler = new ValidateMessageHandler(repository);

            var result = await handler.Handle(new ValidateMessageQuery("pacs.008", "1.0", """{ "amount": "not-a-number" }"""), CancellationToken.None);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Handle_WithUnknownMessageType_ReturnsInvalidWithNotRegisteredError()
        {
            var handler = new ValidateMessageHandler(new FakeMessageTypeRepository());

            var result = await handler.Handle(new ValidateMessageQuery("unknown", "1.0", "{}"), CancellationToken.None);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Contains("not registered"));
        }
    }
}
