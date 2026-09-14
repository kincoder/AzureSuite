using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ingestion.Application.Tests.Messages.Commands.IngestMessage
{
    public class IngestMessageCommandHandlerTests
    {
        private sealed class FakeMessagePublisher : IMessagePublisher
        {
            public Guid? PublishedMessageId { get; private set; }
            public string? PublishedMessageType { get; private set; }
            public string? PublishedVersion { get; private set; }
            public string? PublishedPayload { get; private set; }

            public Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
            {
                PublishedMessageId = messageId;
                PublishedMessageType = messageType;
                PublishedVersion = version;
                PublishedPayload = payload;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Handle_PublishesTheMessageAndReturnsItsMintedId()
        {
            var publisher = new FakeMessagePublisher();
            var handler = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler(publisher, NullLogger<AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler>.Instance);
            var command = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommand("pacs.008", "1.0", "{}");

            var result = await handler.Handle(command, CancellationToken.None);

            result.MessageType.Should().Be("pacs.008");
            result.Version.Should().Be("1.0");
            result.MessageId.Should().NotBeEmpty();
            publisher.PublishedMessageId.Should().Be(result.MessageId);
            publisher.PublishedMessageType.Should().Be("pacs.008");
            publisher.PublishedVersion.Should().Be("1.0");
            publisher.PublishedPayload.Should().Be("{}");
        }

        [Fact]
        public async Task Handle_MintsADifferentIdForEachCall()
        {
            var publisher = new FakeMessagePublisher();
            var handler = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler(publisher, NullLogger<AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler>.Instance);
            var command = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommand("pacs.008", "1.0", "{}");

            var first = await handler.Handle(command, CancellationToken.None);
            var second = await handler.Handle(command, CancellationToken.None);

            first.MessageId.Should().NotBe(second.MessageId);
        }
    }
}
