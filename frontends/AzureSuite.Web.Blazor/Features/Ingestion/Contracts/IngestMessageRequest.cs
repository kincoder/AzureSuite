namespace AzureSuite.Web.Blazor.Features.Ingestion.Contracts
{
    public class IngestMessageRequest
    {
        public string MessageType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
