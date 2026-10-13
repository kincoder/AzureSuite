using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace Processing.Application.Tests.TestDoubles
{
    /// <summary>In-memory <see cref="ILifecycleEventStore"/> that records every call, in order,
    /// into an optional shared call log so tests can assert sequencing across fakes.</summary>
    public class FakeLifecycleEventStore : ILifecycleEventStore
    {
        private readonly List<string> _callLog;

        public FakeLifecycleEventStore(List<string>? callLog = null)
        {
            _callLog = callLog ?? new List<string>();
        }

        public List<LifecycleEvent> RecordedEvents { get; } = new();

        public DateTime? PurgeCutoffUtc { get; private set; }

        public int PurgeCountToReturn { get; set; }

        public Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
        {
            _callLog.Add($"record:{lifecycleEvent.MessageId}");
            RecordedEvents.Add(lifecycleEvent);
            return Task.CompletedTask;
        }

        public Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            _callLog.Add("purge");
            PurgeCutoffUtc = cutoffUtc;
            return Task.FromResult(PurgeCountToReturn);
        }
    }
}
