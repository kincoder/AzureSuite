namespace AzureSuite.Processing.Functions
{
    /// <summary>Tuning for the nightly housekeeping run, bound from the <c>Housekeeping</c>
    /// configuration section (app settings <c>Housekeeping__RetentionDays</c> etc. in Azure).
    /// The schedule itself is not here: it's the separate <c>HousekeepingSchedule</c> setting,
    /// because the timer trigger reads it through a <c>%...%</c> binding expression, not
    /// through options.</summary>
    public class HousekeepingOptions
    {
        public const string SectionName = "Housekeeping";

        /// <summary>Lifecycle events older than this many days are purged.</summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>Most dead letters one run will drain; anything beyond waits for the next run.</summary>
        public int MaxDeadLettersPerRun { get; set; } = 500;
    }
}
