using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RegisterClient : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        private CreateClientRequest Request { get; } = new();

        private EditContext EditContext { get; set; } = default!;

        private string? ErrorMessage;

        protected override void OnInitialized()
        {
            EditContext = new EditContext(Request);
        }

        private async Task SubmitAsync()
        {
            try
            {
                await ApiClient.CreateClientAsync(Request, CancellationToken.None);
                Navigation.NavigateTo("/catalog/clients");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
