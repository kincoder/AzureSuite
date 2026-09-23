using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using AzureSuite.Web.Blazor.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    /// <summary>Presents a form to register a new message type against the catalog.</summary>
    public partial class RegisterMessageType : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        private RegisterMessageTypeRequest Request { get; } = new();

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
                var dto = await ApiClient.RegisterMessageTypeAsync(Request, CancellationToken.None);
                SubmittedMessage = $"Registered {dto.Name} v{dto.Version}.";
                ErrorMessage = null;
                Request.Name = string.Empty;
                Request.Version = string.Empty;
                Request.SchemaDefinition = string.Empty;
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to register the message type. It may already exist, or the server is unavailable.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
