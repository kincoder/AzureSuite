using AzureSuite.Catalog.Application.MessageTypes;
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

        private IReadOnlyList<ClientDto>? Clients;
        private IReadOnlyList<MessageTypeDto>? MessageTypes;

        // Bound to <select> elements, whose values are strings; parsed back to Ids on submit.
        private string SelectedClientId = "";
        private string SelectedMessageTypeId = "";
        private string QueueNamesText = "";
        private EditContext EditContext = default!;
        private string? ErrorMessage;

        protected override async Task OnInitializedAsync()
        {
            EditContext = new EditContext(this);

            try
            {
                var clientsTask = ApiClient.GetClientsAsync(CancellationToken.None);
                var messageTypesTask = ApiClient.GetMessageTypesAsync(CancellationToken.None);
                Clients = await clientsTask;
                MessageTypes = (await messageTypesTask).OrderBy(m => m.Name).ThenBy(m => m.Version).ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load clients and message types: {ex.Message}";
            }
        }

        private async Task SubmitAsync()
        {
            ErrorMessage = null;

            var messageType = MessageTypes?.FirstOrDefault(m => m.Id.ToString() == SelectedMessageTypeId);
            var queueNames = QueueNamesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!Guid.TryParse(SelectedClientId, out var clientId) || messageType is null)
            {
                ErrorMessage = "Choose a client and a message type.";
                return;
            }

            if (queueNames.Length == 0)
            {
                ErrorMessage = "Enter at least one queue name.";
                return;
            }

            try
            {
                var request = new CreateRouteRequest(clientId, messageType.Name, messageType.Version, queueNames);
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
