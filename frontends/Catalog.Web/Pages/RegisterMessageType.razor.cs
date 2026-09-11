using AzureSuite.Catalog.Web.Contracts;
using AzureSuite.Catalog.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    /// <summary>Presents a form to register a new message type against the catalog.</summary>
    public partial class RegisterMessageType : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        private RegisterMessageTypeRequest Request { get; set; } = new();

        private string? SubmittedMessage { get; set; }

        private string? ErrorMessage { get; set; }

        private async Task SubmitAsync()
        {
            try
            {
                var dto = await ApiClient.RegisterMessageTypeAsync(Request, CancellationToken.None);
                SubmittedMessage = $"Registered {dto.Name} v{dto.Version}.";
                ErrorMessage = null;
                Request = new RegisterMessageTypeRequest();
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to register the message type. It may already exist, or the server is unavailable.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
