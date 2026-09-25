using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RoutesList : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;

        private IReadOnlyList<RouteDto>? Routes;
        private Dictionary<Guid, string> ClientNames = new();

        protected override async Task OnInitializedAsync()
        {
            var routesTask = ApiClient.GetRoutesAsync(CancellationToken.None);
            var clientsTask = ApiClient.GetClientsAsync(CancellationToken.None);
            ClientNames = (await clientsTask).ToDictionary(c => c.Id, c => c.Name);
            Routes = await routesTask;
        }

        // The two lists are fetched separately, so a client created or deleted in between
        // may be missing from ClientNames; show its Id rather than a blank cell.
        private string ClientName(Guid clientId) =>
            ClientNames.TryGetValue(clientId, out var name) ? name : clientId.ToString();
    }
}
