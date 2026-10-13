using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Infrastructure.Messaging;
using FluentAssertions;
using Processing.Infrastructure.Tests.TestDoubles;

namespace Processing.Infrastructure.Tests.Messaging
{
    public class ServiceBusDeadLetterQueueTests
    {
        private static ServiceBusReceivedMessage DeadLetter(string messageId, IDictionary<string, object>? properties = null, string body = "{}") =>
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(body),
                messageId: messageId,
                properties: properties ?? new Dictionary<string, object>());

        private static IEnumerable<ServiceBusReceivedMessage> DeadLetters(int count) =>
            Enumerable.Range(1, count).Select(i => DeadLetter($"msg-{i}"));

        [Fact]
        public void ToDeadLetteredMessage_ReadsIngestionPropertiesAndTheDeadLetterReason()
        {
            // Service Bus stores the dead-letter reason/description as application properties
            // with these well-known names; the SDK's DeadLetterReason accessors read them back.
            var message = DeadLetter("msg-1", new Dictionary<string, object>
            {
                ["messageType"] = "pacs.008",
                ["version"] = "1.0",
                ["DeadLetterReason"] = "MalformedMessage",
                ["DeadLetterErrorDescription"] = "Missing version."
            }, body: "12345");

            var result = ServiceBusDeadLetterQueue.ToDeadLetteredMessage(message);

            result.Should().Be(new DeadLetteredMessage("msg-1", "pacs.008", "1.0", "MalformedMessage", "Missing version.", 5));
        }

        [Fact]
        public void ToDeadLetteredMessage_LeavesAbsentPropertiesNull()
        {
            var result = ServiceBusDeadLetterQueue.ToDeadLetteredMessage(DeadLetter("msg-1"));

            result.MessageType.Should().BeNull();
            result.Version.Should().BeNull();
            result.DeadLetterReason.Should().BeNull();
        }

        [Fact]
        public async Task DrainAsync_OpensAReceiverOnTheQueuesDeadLetterSubQueue()
        {
            var client = new FakeServiceBusClient(new FakeServiceBusReceiver(DeadLetters(0)));

            await new ServiceBusDeadLetterQueue(client, "messages.raw").DrainAsync((_, _) => Task.CompletedTask, 10, CancellationToken.None);

            client.RequestedQueueName.Should().Be("messages.raw");
            client.RequestedOptions!.SubQueue.Should().Be(SubQueue.DeadLetter);
            client.Receiver.Disposed.Should().BeTrue();
        }

        [Fact]
        public async Task DrainAsync_HandlesThenCompletesEveryMessageUntilTheQueueIsEmpty()
        {
            var client = new FakeServiceBusClient(new FakeServiceBusReceiver(DeadLetters(3)));
            var handled = new List<string>();

            var drained = await new ServiceBusDeadLetterQueue(client, "messages.raw").DrainAsync(
                (message, _) => { handled.Add(message.MessageId); return Task.CompletedTask; },
                10,
                CancellationToken.None);

            drained.Should().Be(3);
            handled.Should().Equal("msg-1", "msg-2", "msg-3");
            client.Receiver.Completed.Should().Equal("msg-1", "msg-2", "msg-3");
        }

        [Fact]
        public async Task DrainAsync_StopsAtTheCapAndLeavesTheRestForTheNextRun()
        {
            var client = new FakeServiceBusClient(new FakeServiceBusReceiver(DeadLetters(5)));

            var drained = await new ServiceBusDeadLetterQueue(client, "messages.raw").DrainAsync((_, _) => Task.CompletedTask, 2, CancellationToken.None);

            drained.Should().Be(2);
            client.Receiver.Completed.Should().Equal("msg-1", "msg-2");
            client.Receiver.RequestedBatchSizes.Should().OnlyContain(size => size <= 2);
        }

        [Fact]
        public async Task DrainAsync_WhenTheHandlerFails_AbandonsThatMessageAndRethrows()
        {
            var client = new FakeServiceBusClient(new FakeServiceBusReceiver(DeadLetters(3)));

            var act = () => new ServiceBusDeadLetterQueue(client, "messages.raw").DrainAsync(
                (message, _) => message.MessageId == "msg-2" ? throw new InvalidOperationException("Cosmos unavailable") : Task.CompletedTask,
                10,
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            client.Receiver.Completed.Should().Equal("msg-1");
            client.Receiver.Abandoned.Should().Equal("msg-2");
        }
    }
}
