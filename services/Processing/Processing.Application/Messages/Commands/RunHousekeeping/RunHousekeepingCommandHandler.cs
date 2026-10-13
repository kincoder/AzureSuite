using AzureSuite.Processing.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping
{
    /// <summary>Runs the two housekeeping steps in order. Sweeping comes first so that dead
    /// letters are logged before anything is deleted; the events it writes are brand new and so
    /// can never fall inside the purge window of the same run.</summary>
    public class RunHousekeepingCommandHandler : IRequestHandler<RunHousekeepingCommand, HousekeepingReport>
    {
        private readonly IDeadLetterQueue _deadLetterQueue;
        private readonly ILifecycleEventStore _store;
        private readonly ILogger<RunHousekeepingCommandHandler> _logger;

        public RunHousekeepingCommandHandler(IDeadLetterQueue deadLetterQueue, ILifecycleEventStore store, ILogger<RunHousekeepingCommandHandler> logger)
        {
            _deadLetterQueue = deadLetterQueue;
            _store = store;
            _logger = logger;
        }

        public async Task<HousekeepingReport> Handle(RunHousekeepingCommand request, CancellationToken cancellationToken)
        {
            if (request.RetentionDays < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request), request.RetentionDays, "RetentionDays must be at least 1; 0 would purge the whole lifecycle log.");
            }

            var swept = await _deadLetterQueue.DrainAsync(
                (message, ct) => _store.RecordAsync(ToLifecycleEvent(message, request.NowUtc), ct),
                request.MaxDeadLettersPerRun,
                cancellationToken);

            var cutoffUtc = request.NowUtc.AddDays(-request.RetentionDays);
            var purged = await _store.PurgeOlderThanAsync(cutoffUtc, cancellationToken);

            _logger.LogInformation(
                "Housekeeping swept {DeadLettersSwept} dead letters and purged {EventsPurged} lifecycle events older than {PurgeCutoffUtc:o}",
                swept, purged, cutoffUtc);

            return new HousekeepingReport(swept, purged, cutoffUtc);
        }

        private static LifecycleEvent ToLifecycleEvent(DeadLetteredMessage message, DateTime nowUtc)
        {
            var reason = message.DeadLetterErrorDescription is null
                ? message.DeadLetterReason
                : $"{message.DeadLetterReason}: {message.DeadLetterErrorDescription}";

            return new LifecycleEvent(
                message.MessageId,
                LifecycleStatus.DeadLettered,
                message.MessageType,
                message.Version,
                reason,
                message.PayloadSizeBytes,
                nowUtc);
        }
    }
}
