using AzureSuite.Web.Blazor.Features.Ingestion.Contracts;
using AzureSuite.Web.Blazor.Features.Ingestion.Services;
using AzureSuite.Web.Blazor.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Ingestion.Pages
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

        private IngestMessageRequest Request { get; } = new();

        private EditContext EditContext { get; set; } = null!;

        private string? SubmittedMessage { get; set; }

        private string? ErrorMessage { get; set; }

        protected override void OnInitialized()
        {
            EditContext = new EditContext(Request);
            EditContext.OnFieldChanged += (_, _) =>
            {
                ErrorMessage = null;
                SubmittedMessage = null;
                StateHasChanged();
            };
        }

        private async Task SubmitAsync()
        {
            try
            {
                var dto = await ApiClient.SubmitMessageAsync(Request, CancellationToken.None);
                SubmittedMessage = $"Accepted as {dto.MessageId}.";
                ErrorMessage = null;
                Request.MessageType = string.Empty;
                Request.Version = string.Empty;
                Request.Payload = string.Empty;
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to submit the message. Check the required fields, or the server may be unavailable.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
