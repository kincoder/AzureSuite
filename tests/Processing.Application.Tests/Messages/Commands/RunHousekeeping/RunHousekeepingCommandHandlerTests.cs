using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Processing.Application.Tests.TestDoubles;

namespace Processing.Application.Tests.Messages.Commands.RunHousekeeping
{
    public class RunHousekeepingCommandHandlerTests
    {
        private static readonly DateTime Now = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

        private static RunHousekeepingCommandHandler NewHandler(FakeDeadLetterQueue deadLetterQueue, FakeLifecycleEventStore store) =>
            new(deadLetterQueue, store, NullLogger<RunHousekeepingCommandHandler>.Instance);

        [Fact]
        public async Task Handle_RecordsEachDeadLetterAsADeadLetteredEvent()
        {
            var deadLetterQueue = new FakeDeadLetterQueue();
            deadLetterQueue.Messages.Add(new DeadLetteredMessage("msg-1", "pacs.008", "1.0", "MalformedMessage", "Missing version.", 10));
            deadLetterQueue.Messages.Add(new DeadLetteredMessage("msg-2", null, null, "MaxDeliveryCountExceeded", null, 20));
            var store = new FakeLifecycleEventStore();

            await NewHandler(deadLetterQueue, store).Handle(new RunHousekeepingCommand(Now, 30, 500), CancellationToken.None);

            store.RecordedEvents.Should().Equal(
                new LifecycleEvent("msg-1", LifecycleStatus.DeadLettered, "pacs.008", "1.0", "MalformedMessage: Missing version.", 10, Now),
                new LifecycleEvent("msg-2", LifecycleStatus.DeadLettered, null, null, "MaxDeliveryCountExceeded", 20, Now));
        }

        [Fact]
        public async Task Handle_PurgesEventsOlderThanTheRetentionWindow()
        {
            var store = new FakeLifecycleEventStore();

            await NewHandler(new FakeDeadLetterQueue(), store).Handle(new RunHousekeepingCommand(Now, 30, 500), CancellationToken.None);

            store.PurgeCutoffUtc.Should().Be(Now.AddDays(-30));
        }

        [Fact]
        public async Task Handle_SweepsTheDeadLetterQueueBeforePurging()
        {
            var callLog = new List<string>();
            var deadLetterQueue = new FakeDeadLetterQueue(callLog);
            deadLetterQueue.Messages.Add(new DeadLetteredMessage("msg-1", null, null, null, null, 0));
            var store = new FakeLifecycleEventStore(callLog);

            await NewHandler(deadLetterQueue, store).Handle(new RunHousekeepingCommand(Now, 30, 500), CancellationToken.None);

            callLog.Should().Equal("drain", "record:msg-1", "purge");
        }

        [Fact]
        public async Task Handle_PassesTheDeadLetterCapThrough()
        {
            var deadLetterQueue = new FakeDeadLetterQueue();

            await NewHandler(deadLetterQueue, new FakeLifecycleEventStore()).Handle(new RunHousekeepingCommand(Now, 30, 25), CancellationToken.None);

            deadLetterQueue.RequestedMaxMessages.Should().Be(25);
        }

        [Fact]
        public async Task Handle_ReportsWhatTheRunDid()
        {
            var deadLetterQueue = new FakeDeadLetterQueue();
            deadLetterQueue.Messages.Add(new DeadLetteredMessage("msg-1", null, null, null, null, 0));
            deadLetterQueue.Messages.Add(new DeadLetteredMessage("msg-2", null, null, null, null, 0));
            var store = new FakeLifecycleEventStore { PurgeCountToReturn = 7 };

            var report = await NewHandler(deadLetterQueue, store).Handle(new RunHousekeepingCommand(Now, 30, 500), CancellationToken.None);

            report.Should().Be(new HousekeepingReport(DeadLettersSwept: 2, EventsPurged: 7, PurgeCutoffUtc: Now.AddDays(-30)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Handle_WithRetentionBelowOneDay_ThrowsWithoutTouchingAnything(int retentionDays)
        {
            var callLog = new List<string>();
            var handler = NewHandler(new FakeDeadLetterQueue(callLog), new FakeLifecycleEventStore(callLog));

            var act = () => handler.Handle(new RunHousekeepingCommand(Now, retentionDays, 500), CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            callLog.Should().BeEmpty();
        }
    }
}
