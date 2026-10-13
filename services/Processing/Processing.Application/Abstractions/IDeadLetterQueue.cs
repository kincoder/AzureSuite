using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Application.Abstractions
{
    /// <summary>Access to the inbound queue's dead-letter sub-queue.</summary>
    public interface IDeadLetterQueue
    {
        /// <summary>Hands each dead-lettered message to <paramref name="handler"/>, removing it
        /// from the dead-letter queue only once the handler has returned. If the handler throws,
        /// that message is released back to the queue (to be picked up by the next run) and the
        /// exception propagates.</summary>
        /// <param name="maxMessages">Upper bound for one run, so a flooded dead-letter queue
        /// can't keep a single invocation running indefinitely.</param>
        /// <returns>How many messages were drained.</returns>
        Task<int> DrainAsync(
            Func<DeadLetteredMessage, CancellationToken, Task> handler,
            int maxMessages,
            CancellationToken cancellationToken);
    }
}
