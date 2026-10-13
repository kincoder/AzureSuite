using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace Processing.Application.Tests.TestDoubles
{
    /// <summary>Hands out a fixed list of dead letters, honouring the max-messages cap the way
    /// the real implementation does.</summary>
    public class FakeDeadLetterQueue : IDeadLetterQueue
    {
        private readonly List<string> _callLog;

        public FakeDeadLetterQueue(List<string>? callLog = null)
        {
            _callLog = callLog ?? new List<string>();
        }

        public List<DeadLetteredMessage> Messages { get; } = new();

        public int? RequestedMaxMessages { get; private set; }

        public async Task<int> DrainAsync(
            Func<DeadLetteredMessage, CancellationToken, Task> handler,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            _callLog.Add("drain");
            RequestedMaxMessages = maxMessages;

            var drained = 0;
            foreach (var message in Messages.Take(maxMessages))
            {
                await handler(message, cancellationToken);
                drained++;
            }

            return drained;
        }
    }
}
