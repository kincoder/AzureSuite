namespace AzureSuite.Ingestion.Api.Contracts
{
    /// <summary>Request body for <c>POST /messages</c>. All three fields are required — this
    /// is the only validation Ingestion performs; see the Ingestion design spec for why
    /// deeper validation is deliberately not done here.</summary>
    public record IngestMessageRequest(string MessageType, string Version, string Payload);
}
