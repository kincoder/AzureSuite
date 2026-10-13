using AzureSuite.Processing.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage
{
    /// <summary>Writes a <see cref="LifecycleStatus.Received"/> event for the message.</summary>
    public class RecordInboundMessageCommandHandler : IRequestHandler<RecordInboundMessageCommand>
    {
        private readonly ILifecycleEventStore _store;
        private readonly ILogger<RecordInboundMessageCommandHandler> _logger;

        public RecordInboundMessageCommandHandler(ILifecycleEventStore store, ILogger<RecordInboundMessageCommandHandler> logger)
        {
            _store = store;
            _logger = logger;
        }

        public async Task Handle(RecordInboundMessageCommand request, CancellationToken cancellationToken)
        {
            var lifecycleEvent = new LifecycleEvent(
                request.MessageId,
                LifecycleStatus.Received,
                request.MessageType,
                request.Version,
                Reason: null,
                request.PayloadSizeBytes,
                request.ReceivedAtUtc);

            await _store.RecordAsync(lifecycleEvent, cancellationToken);

            _logger.LogInformation(
                "Recorded message {MessageId} of type {MessageType} v{Version} as {Status}",
                request.MessageId, request.MessageType, request.Version, LifecycleStatus.Received);
        }
    }
}
