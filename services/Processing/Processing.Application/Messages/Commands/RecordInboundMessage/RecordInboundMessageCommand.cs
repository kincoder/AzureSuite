using MediatR;

namespace AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage
{
    /// <summary>Logs that a message was received from <c>messages.raw</c>. This is the whole
    /// of Processing's per-message work in this increment; validation, route lookup and fan-out
    /// from the Routing &amp; Processing spec slot in here later.</summary>
    public record RecordInboundMessageCommand(
        string MessageId,
        string MessageType,
        string Version,
        int PayloadSizeBytes,
        DateTime ReceivedAtUtc) : IRequest;
}
