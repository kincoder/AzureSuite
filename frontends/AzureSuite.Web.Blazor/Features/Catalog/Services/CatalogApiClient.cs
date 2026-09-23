using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;

namespace AzureSuite.Web.Blazor.Features.Catalog.Services
{
    /// <summary>Thin wrapper over Catalog.Api's HTTP endpoints.</summary>
    public class CatalogApiClient
    {
        private readonly HttpClient _httpClient;

        public CatalogApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>Retrieves all message types registered in the catalog.</summary>
        public async Task<IReadOnlyList<MessageTypeDto>> GetMessageTypesAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetFromJsonAsync<List<MessageTypeDto>>("/message-types", cancellationToken);
            return result ?? new List<MessageTypeDto>();
        }

        /// <summary>Registers a new message type in the catalog.</summary>
        public async Task<MessageTypeDto> RegisterMessageTypeAsync(RegisterMessageTypeRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/message-types", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<MessageTypeDto>(cancellationToken))!;
        }
    }
}
