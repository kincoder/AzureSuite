using System.Net.Http.Json;
using AzureSuite.Catalog.Application.MessageTypes;
using AzureSuite.Catalog.Web.Contracts;

namespace AzureSuite.Catalog.Web.Services
{
    /// <summary>Thin wrapper over Catalog.Api's HTTP endpoints.</summary>
    public class CatalogApiClient
    {
        private readonly HttpClient _httpClient;

        public CatalogApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<MessageTypeDto>> GetMessageTypesAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetFromJsonAsync<List<MessageTypeDto>>("/message-types", cancellationToken);
            return result ?? new List<MessageTypeDto>();
        }

        public async Task<MessageTypeDto> RegisterMessageTypeAsync(RegisterMessageTypeRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/message-types", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<MessageTypeDto>(cancellationToken))!;
        }
    }
}
