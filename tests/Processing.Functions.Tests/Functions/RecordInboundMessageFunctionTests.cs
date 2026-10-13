using AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage;
using AzureSuite.Processing.Functions.Functions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Processing.Functions.Tests.TestDoubles;

namespace Processing.Functions.Tests.Functions
{
    public class RecordInboundMessageFunctionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 30, 0, TimeSpan.Zero);

        private static RecordInboundMessageFunction NewFunction(FakeSender sender) =>
            new(sender, new FakeTimeProvider(Now), NullLogger<RecordInboundMessageFunction>.Instance);

        [Fact]
        public async Task Run_WithAWellFormedMessage_RecordsItThenCompletesIt()
        {
            var sender = new FakeSender();
            var actions = new FakeServiceBusMessageActions();

            await NewFunction(sender).Run(InboundMessageReaderTests.Message("pacs.008", "1.0", "12345"), actions, CancellationToken.None);

            sender.Sent.Should().ContainSingle().Which.Should().Be(
                new RecordInboundMessageCommand("msg-1", "pacs.008", "1.0", 5, Now.UtcDateTime));
            actions.Completed.Should().Equal("msg-1");
            actions.DeadLettered.Should().BeEmpty();
        }

        [Fact]
        public async Task Run_WithAMalformedMessage_DeadLettersItWithoutRecording()
        {
            var sender = new FakeSender();
            var actions = new FakeServiceBusMessageActions();

            await NewFunction(sender).Run(InboundMessageReaderTests.Message("pacs.008", null), actions, CancellationToken.None);

            sender.Sent.Should().BeEmpty();
            actions.Completed.Should().BeEmpty();
            actions.DeadLettered.Should().ContainSingle().Which.Should().Match<(string MessageId, string? Reason, string? Description)>(d =>
                d.MessageId == "msg-1" &&
                d.Reason == RecordInboundMessageFunction.MalformedMessageReason &&
                d.Description!.Contains("version"));
        }

        [Fact]
        public async Task Run_WhenRecordingFails_PropagatesAndLeavesTheMessageUnsettled()
        {
            // Unsettled + exception = the host abandons it and Service Bus redelivers.
            var sender = new FakeSender { ExceptionToThrow = new InvalidOperationException("Cosmos unavailable") };
            var actions = new FakeServiceBusMessageActions();

            var act = () => NewFunction(sender).Run(InboundMessageReaderTests.Message("pacs.008", "1.0"), actions, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            actions.Completed.Should().BeEmpty();
            actions.DeadLettered.Should().BeEmpty();
        }
    }
}
