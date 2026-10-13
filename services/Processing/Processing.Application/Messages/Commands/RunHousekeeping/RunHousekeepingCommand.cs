using MediatR;

namespace AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping
{
    /// <summary>The nightly clean-up: sweep the dead-letter queue into the lifecycle log, then
    /// purge lifecycle events older than the retention window.</summary>
    /// <param name="NowUtc">The run's reference time, passed in rather than read from the clock
    /// so the cutoff is deterministic under test.</param>
    /// <param name="RetentionDays">How many days of lifecycle events to keep.</param>
    /// <param name="MaxDeadLettersPerRun">Cap on how many dead letters one run drains.</param>
    public record RunHousekeepingCommand(DateTime NowUtc, int RetentionDays, int MaxDeadLettersPerRun) : IRequest<HousekeepingReport>;
}
