using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class ClientsList : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;

        private IReadOnlyList<ClientDto>? Clients;

        protected override async Task OnInitializedAsync()
        {
            Clients = await ApiClient.GetClientsAsync(CancellationToken.None);
        }
    }
}
