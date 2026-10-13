using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;
using Microsoft.Azure.Cosmos;

namespace AzureSuite.Processing.Infrastructure.Persistence
{
    /// <summary>Stores lifecycle events in the <c>LifecycleEvents</c> container, partitioned by
    /// <c>/messageId</c> (see <c>infra/modules/shared/cosmos.bicep</c>) so every event for one
    /// message lives in the same logical partition.</summary>
    public class CosmosLifecycleEventStore : ILifecycleEventStore
    {
        /// <summary>The stored shape. Property names are pinned with <see cref="JsonPropertyNameAttribute"/>
        /// because Cosmos requires the identifier to be exactly <c>id</c> (lowercase) and the
        /// partition key path must match <c>/messageId</c>.</summary>
        public record LifecycleEventDocument(
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("messageId")] string MessageId,
            [property: JsonPropertyName("status")] string Status,
            [property: JsonPropertyName("messageType")] string? MessageType,
            [property: JsonPropertyName("version")] string? Version,
            [property: JsonPropertyName("reason")] string? Reason,
            [property: JsonPropertyName("payloadSizeBytes")] int PayloadSizeBytes,
            [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc);

        private record PurgeCandidate(
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("messageId")] string MessageId);

        private static readonly char[] CharactersNotAllowedInId = ['/', '\\', '?', '#'];

        private readonly Container _container;

        public CosmosLifecycleEventStore(Container container)
        {
            _container = container;
        }

        /// <summary>Client options this store relies on: System.Text.Json serialization, so the
        /// <see cref="JsonPropertyNameAttribute"/>s on <see cref="LifecycleEventDocument"/> are
        /// honoured (the SDK's default Newtonsoft serializer would ignore them).</summary>
        public static CosmosClientOptions CreateClientOptions()
        {
            return new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            };
        }

        /// <summary>One document per (message, status) pair. Deriving the id from that pair is
        /// what makes <see cref="RecordAsync"/> idempotent: a redelivered message upserts over
        /// its earlier entry instead of adding a second one.</summary>
        public static string DocumentId(string messageId, string status)
        {
            var safeMessageId = string.Join('_', messageId.Split(CharactersNotAllowedInId));
            return $"{safeMessageId}:{status}";
        }

        public static LifecycleEventDocument ToDocument(LifecycleEvent lifecycleEvent)
        {
            return new LifecycleEventDocument(
                DocumentId(lifecycleEvent.MessageId, lifecycleEvent.Status),
                lifecycleEvent.MessageId,
                lifecycleEvent.Status,
                lifecycleEvent.MessageType,
                lifecycleEvent.Version,
                lifecycleEvent.Reason,
                lifecycleEvent.PayloadSizeBytes,
                lifecycleEvent.OccurredAtUtc);
        }

        /// <summary>Selects events by Cosmos's own <c>_ts</c> (last-write time, epoch seconds)
        /// rather than <c>occurredAtUtc</c>: <c>_ts</c> is a number, so the comparison is exact,
        /// whereas ISO-8601 strings with varying fractional-second precision don't sort
        /// reliably as text.</summary>
        public static QueryDefinition BuildPurgeQuery(DateTime cutoffUtc)
        {
            var cutoffEpochSeconds = new DateTimeOffset(DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return new QueryDefinition("SELECT c.id, c.messageId FROM c WHERE c._ts < @cutoff")
                .WithParameter("@cutoff", cutoffEpochSeconds);
        }

        public async Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
        {
            var document = ToDocument(lifecycleEvent);
            await _container.UpsertItemAsync(document, new PartitionKey(document.MessageId), cancellationToken: cancellationToken);
        }

        public async Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            var purged = 0;
            using var iterator = _container.GetItemQueryIterator<PurgeCandidate>(BuildPurgeQuery(cutoffUtc));

            while (iterator.HasMoreResults)
            {
                foreach (var candidate in await iterator.ReadNextAsync(cancellationToken))
                {
                    using var response = await _container.DeleteItemStreamAsync(
                        candidate.Id, new PartitionKey(candidate.MessageId), cancellationToken: cancellationToken);

                    // Already gone (e.g. two overlapping runs) is the outcome we wanted anyway.
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    purged++;
                }
            }

            return purged;
        }
    }
}
