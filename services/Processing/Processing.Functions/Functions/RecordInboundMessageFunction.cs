using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Processing.Functions.Functions
{
    /// <summary>Service Bus–triggered: runs once per message arriving on the inbound queue
    /// (<c>messages.raw</c>, from the <c>InboundQueueName</c> setting) and logs it to Cosmos.
    ///
    /// Settlement is explicit (<c>AutoCompleteMessages = false</c>), giving three outcomes:
    /// <list type="bullet">
    /// <item>well-formed and logged → completed (removed from the queue);</item>
    /// <item>malformed → dead-lettered immediately, since retrying can't fix it;</item>
    /// <item>anything throws (e.g. Cosmos unavailable) → left unsettled, so the host abandons it
    /// and Service Bus redelivers, up to the queue's max delivery count (10) before
    /// dead-lettering it itself.</item>
    /// </list>
    /// Either way, dead letters are picked up by <see cref="RunHousekeepingFunction"/>.</summary>
    public class RecordInboundMessageFunction
    {
        public const string MalformedMessageReason = "MalformedMessage";

        private readonly ISender _sender;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecordInboundMessageFunction> _logger;

        public RecordInboundMessageFunction(ISender sender, TimeProvider timeProvider, ILogger<RecordInboundMessageFunction> logger)
        {
            _sender = sender;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        [Function("RecordInboundMessage")]
        public async Task Run(
            [ServiceBusTrigger("%InboundQueueName%", Connection = "ServiceBus", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            if (!InboundMessageReader.TryRead(message, _timeProvider.GetUtcNow().UtcDateTime, out var command, out var problem))
            {
                _logger.LogWarning("Dead-lettering malformed message {MessageId}: {Problem}", message.MessageId, problem);
                await messageActions.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: MalformedMessageReason,
                    deadLetterErrorDescription: problem,
                    cancellationToken: cancellationToken);
                return;
            }

            await _sender.Send(command, cancellationToken);
            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
    }
}
