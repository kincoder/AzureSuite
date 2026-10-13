namespace AzureSuite.Processing.Application.Messages
{
    /// <summary>The lifecycle states Processing records for a message. Only the two states this
    /// increment produces exist yet; the full pipeline in the Routing &amp; Processing spec adds
    /// "Rejected" and "Routed" alongside them.</summary>
    public static class LifecycleStatus
    {
        /// <summary>The message was picked up from <c>messages.raw</c> and logged.</summary>
        public const string Received = "Received";

        /// <summary>The message ended up in <c>messages.raw</c>'s dead-letter queue and was
        /// swept out of it by the nightly housekeeping run.</summary>
        public const string DeadLettered = "DeadLettered";
    }
}
