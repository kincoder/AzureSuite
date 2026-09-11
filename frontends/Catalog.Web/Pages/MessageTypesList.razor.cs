using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    /// <summary>Displays the list of message types registered in the catalog.</summary>
    public partial class MessageTypesList : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        private IReadOnlyList<MessageTypeDto>? MessageTypes { get; set; }

        private string? ErrorMessage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                MessageTypes = await ApiClient.GetMessageTypesAsync(CancellationToken.None);
            }
            catch (HttpRequestException)
            {
                ErrorMessage = "Failed to load message types. Please try again later.";
            }
        }
    }
}
