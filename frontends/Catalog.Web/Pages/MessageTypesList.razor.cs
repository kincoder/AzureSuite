using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    /// <summary>Displays the list of message types registered in the catalog.</summary>
    public partial class MessageTypesList : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        private IReadOnlyList<MessageTypeDto>? MessageTypes { get; set; }

        private string? ErrorMessage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                MessageTypes = await ApiClient.GetMessageTypesAsync(CancellationToken.None);
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to load message types. Please try again later.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
