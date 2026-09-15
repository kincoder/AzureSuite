using AzureSuite.Ingestion.Web.Contracts;
using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Ingestion.Web.Pages
{
    /// <summary>Presents a form to submit a test message to Ingestion.Api. Serves as the
    /// interim producer-simulation tool per the master design spec, ahead of a real
    /// diagnostics dashboard once downstream lifecycle data exists to back one.</summary>
    public partial class SubmitMessage : ComponentBase
    {
        [Inject]
        private IngestionApiClient ApiClient { get; set; } = null!;

        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        private IngestMessageRequest Request { get; set; } = new();

        private string? SubmittedMessage { get; set; }

        private string? ErrorMessage { get; set; }

        private async Task SubmitAsync()
        {
            try
            {
                var dto = await ApiClient.SubmitMessageAsync(Request, CancellationToken.None);
                SubmittedMessage = $"Accepted as {dto.MessageId}.";
                ErrorMessage = null;
                Request = new IngestMessageRequest();
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to submit the message. Check the required fields, or the server may be unavailable.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
