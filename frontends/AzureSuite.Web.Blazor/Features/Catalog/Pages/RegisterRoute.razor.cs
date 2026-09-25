using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RegisterRoute : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        private string ClientIdText = "";
        private string MessageTypeName = "";
        private string MessageTypeVersion = "";
        private string QueueNamesText = "";
        private EditContext EditContext = default!;
        private string? ErrorMessage;

        protected override void OnInitialized()
        {
            EditContext = new EditContext(this);
        }

        private async Task SubmitAsync()
        {
            try
            {
                var queueNames = QueueNamesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var request = new CreateRouteRequest(Guid.Parse(ClientIdText), MessageTypeName, MessageTypeVersion, queueNames);
                await ApiClient.CreateRouteAsync(request, CancellationToken.None);
                Navigation.NavigateTo("/catalog/routes");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
