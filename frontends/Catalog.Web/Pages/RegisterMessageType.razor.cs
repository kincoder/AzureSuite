using AzureSuite.Catalog.Web.Contracts;
using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Catalog.Web.Pages
{
    /// <summary>Presents a form to register a new message type against the catalog.</summary>
    public partial class RegisterMessageType : ComponentBase
    {
        [Inject]
        private CatalogApiClient ApiClient { get; set; } = null!;

        private RegisterMessageTypeRequest Request { get; set; } = new();

        private string? SubmittedMessage { get; set; }

        private async Task SubmitAsync()
        {
            var dto = await ApiClient.RegisterMessageTypeAsync(Request, CancellationToken.None);
            SubmittedMessage = $"Registered {dto.Name} v{dto.Version}.";
            Request = new RegisterMessageTypeRequest();
        }
    }
}
