using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Processing.Application.Tests.TestDoubles;

namespace Processing.Application.Tests.Messages.Commands.RecordInboundMessage
{
    public class RecordInboundMessageCommandHandlerTests
    {
        private static readonly DateTime ReceivedAt = new(2026, 10, 13, 9, 30, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Handle_RecordsOneReceivedEventCarryingTheMessageDetails()
        {
            var store = new FakeLifecycleEventStore();
            var handler = new RecordInboundMessageCommandHandler(store, NullLogger<RecordInboundMessageCommandHandler>.Instance);

            await handler.Handle(
                new RecordInboundMessageCommand("msg-1", "pacs.008", "1.0", 42, ReceivedAt),
                CancellationToken.None);

            store.RecordedEvents.Should().ContainSingle().Which.Should().Be(
                new LifecycleEvent("msg-1", LifecycleStatus.Received, "pacs.008", "1.0", null, 42, ReceivedAt));
        }

        [Fact]
        public async Task Handle_DoesNotPurgeAnything()
        {
            var store = new FakeLifecycleEventStore();
            var handler = new RecordInboundMessageCommandHandler(store, NullLogger<RecordInboundMessageCommandHandler>.Instance);

            await handler.Handle(
                new RecordInboundMessageCommand("msg-1", "pacs.008", "1.0", 42, ReceivedAt),
                CancellationToken.None);

            store.PurgeCutoffUtc.Should().BeNull();
        }
    }
}
