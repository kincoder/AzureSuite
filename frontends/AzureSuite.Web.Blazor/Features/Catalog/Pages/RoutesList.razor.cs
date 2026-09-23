using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RoutesList : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;

        private IReadOnlyList<RouteDto>? Routes;

        protected override async Task OnInitializedAsync()
        {
            Routes = await ApiClient.GetRoutesAsync(CancellationToken.None);
        }
    }
}
