using System.Text.Json;
using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Infrastructure.Persistence;
using FluentAssertions;

namespace Processing.Infrastructure.Tests.Persistence
{
    /// <summary>Covers the parts of the store that decide what gets written and what gets
    /// deleted. The calls into <c>Container</c> themselves need a live Cosmos account or the
    /// emulator, neither of which is wired into the test run.</summary>
    public class CosmosLifecycleEventStoreTests
    {
        private static readonly DateTime OccurredAt = new(2026, 10, 13, 9, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void ToDocument_MapsEveryFieldAndKeysTheDocumentByMessageAndStatus()
        {
            var lifecycleEvent = new LifecycleEvent("msg-1", LifecycleStatus.Received, "pacs.008", "1.0", null, 42, OccurredAt);

            var document = CosmosLifecycleEventStore.ToDocument(lifecycleEvent);

            document.Should().Be(new CosmosLifecycleEventStore.LifecycleEventDocument(
                "msg-1:Received", "msg-1", LifecycleStatus.Received, "pacs.008", "1.0", null, 42, OccurredAt));
        }

        [Fact]
        public void DocumentId_IsTheSameForARedeliveryOfTheSameMessage()
        {
            var first = CosmosLifecycleEventStore.DocumentId("msg-1", LifecycleStatus.Received);
            var redelivered = CosmosLifecycleEventStore.DocumentId("msg-1", LifecycleStatus.Received);

            redelivered.Should().Be(first);
        }

        [Fact]
        public void DocumentId_DiffersPerStatus_SoOneMessageCanHaveSeveralEvents()
        {
            CosmosLifecycleEventStore.DocumentId("msg-1", LifecycleStatus.Received)
                .Should().NotBe(CosmosLifecycleEventStore.DocumentId("msg-1", LifecycleStatus.DeadLettered));
        }

        [Fact]
        public void DocumentId_ReplacesCharactersCosmosRejectsInIds()
        {
            CosmosLifecycleEventStore.DocumentId(@"a/b\c?d#e", LifecycleStatus.Received)
                .Should().Be("a_b_c_d_e:Received");
        }

        [Fact]
        public void BuildPurgeQuery_FiltersOnTheLastWriteTimestampInEpochSeconds()
        {
            var cutoff = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);

            var query = CosmosLifecycleEventStore.BuildPurgeQuery(cutoff);

            query.QueryText.Should().Contain("c._ts < @cutoff");
            query.GetQueryParameters().Should().ContainSingle()
                .Which.Should().Be(("@cutoff", (object)new DateTimeOffset(cutoff).ToUnixTimeSeconds()));
        }

        [Fact]
        public void CreateClientOptions_SerializesDocumentsWithTheLowercaseIdCosmosRequires()
        {
            var options = CosmosLifecycleEventStore.CreateClientOptions();
            var document = CosmosLifecycleEventStore.ToDocument(
                new LifecycleEvent("msg-1", LifecycleStatus.Received, "pacs.008", "1.0", null, 42, OccurredAt));

            var json = JsonSerializer.Serialize(document, options.UseSystemTextJsonSerializerWithOptions);

            using var parsed = JsonDocument.Parse(json);
            parsed.RootElement.GetProperty("id").GetString().Should().Be("msg-1:Received");
            parsed.RootElement.GetProperty("messageId").GetString().Should().Be("msg-1");
        }
    }
}
