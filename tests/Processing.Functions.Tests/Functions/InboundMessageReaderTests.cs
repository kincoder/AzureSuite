using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage;
using AzureSuite.Processing.Functions.Functions;
using FluentAssertions;

namespace Processing.Functions.Tests.Functions
{
    public class InboundMessageReaderTests
    {
        private static readonly DateTime ReceivedAt = new(2026, 10, 13, 9, 30, 0, DateTimeKind.Utc);

        public static ServiceBusReceivedMessage Message(string? messageType, string? version, string body = "{\"amount\":100}")
        {
            var properties = new Dictionary<string, object>();
            if (messageType is not null)
            {
                properties["messageType"] = messageType;
            }
            if (version is not null)
            {
                properties["version"] = version;
            }

            return ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(body), messageId: "msg-1", properties: properties);
        }

        [Fact]
        public void TryRead_WithIngestionsProperties_BuildsTheCommand()
        {
            var ok = InboundMessageReader.TryRead(Message("pacs.008", "1.0", "12345"), ReceivedAt, out var command, out var problem);

            ok.Should().BeTrue();
            problem.Should().BeNull();
            command.Should().Be(new RecordInboundMessageCommand("msg-1", "pacs.008", "1.0", 5, ReceivedAt));
        }

        [Theory]
        [InlineData(null, "1.0", "messageType")]
        [InlineData("pacs.008", null, "version")]
        [InlineData("  ", "1.0", "messageType")]
        public void TryRead_WithAMissingProperty_FailsAndNamesIt(string? messageType, string? version, string expectedMissing)
        {
            var ok = InboundMessageReader.TryRead(Message(messageType, version), ReceivedAt, out var command, out var problem);

            ok.Should().BeFalse();
            command.Should().BeNull();
            problem.Should().Contain(expectedMissing);
        }

        [Fact]
        public void TryRead_WithBothPropertiesMissing_NamesBoth()
        {
            InboundMessageReader.TryRead(Message(null, null), ReceivedAt, out _, out var problem);

            problem.Should().Contain("messageType").And.Contain("version");
        }
    }
}
