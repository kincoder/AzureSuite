namespace AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping
{
    /// <summary>What one housekeeping run did.</summary>
    /// <param name="DeadLettersSwept">Messages moved out of the dead-letter queue into the
    /// lifecycle log.</param>
    /// <param name="EventsPurged">Lifecycle events deleted for being past retention.</param>
    /// <param name="PurgeCutoffUtc">Events last written before this instant were purged.</param>
    public record HousekeepingReport(int DeadLettersSwept, int EventsPurged, DateTime PurgeCutoffUtc);
}
