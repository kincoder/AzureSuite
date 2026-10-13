using AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureSuite.Processing.Functions.Functions
{
    /// <summary>Timer-triggered nightly housekeeping. The schedule is the six-field NCRONTAB
    /// expression in the <c>HousekeepingSchedule</c> setting (seconds first):
    /// <c>0 0 0 * * *</c> = every day at 00:00:00. Linux Function Apps evaluate it in UTC.</summary>
    public class RunHousekeepingFunction
    {
        private readonly ISender _sender;
        private readonly TimeProvider _timeProvider;
        private readonly HousekeepingOptions _options;
        private readonly ILogger<RunHousekeepingFunction> _logger;

        public RunHousekeepingFunction(
            ISender sender,
            TimeProvider timeProvider,
            IOptions<HousekeepingOptions> options,
            ILogger<RunHousekeepingFunction> logger)
        {
            _sender = sender;
            _timeProvider = timeProvider;
            _options = options.Value;
            _logger = logger;
        }

        [Function("RunHousekeeping")]
        public async Task<HousekeepingReport> Run(
            [TimerTrigger("%HousekeepingSchedule%")] TimerInfo timer,
            CancellationToken cancellationToken)
        {
            if (timer.IsPastDue)
            {
                // The host was down at the scheduled time and is catching up on the missed run.
                _logger.LogWarning("Housekeeping is running late; the scheduled run was missed");
            }

            return await _sender.Send(
                new RunHousekeepingCommand(
                    _timeProvider.GetUtcNow().UtcDateTime,
                    _options.RetentionDays,
                    _options.MaxDeadLettersPerRun),
                cancellationToken);
        }
    }
}
