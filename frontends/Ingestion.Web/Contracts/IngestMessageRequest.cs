namespace AzureSuite.Ingestion.Web.Contracts
{
    /// <summary>Request body sent to Ingestion.Api's POST /messages endpoint.</summary>
    public class IngestMessageRequest
    {
        public string MessageType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
