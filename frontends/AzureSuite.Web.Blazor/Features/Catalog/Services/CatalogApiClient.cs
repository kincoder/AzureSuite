using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        /// <summary>Retrieves all clients registered in the catalog.</summary>
        public async Task<IReadOnlyList<ClientDto>> GetClientsAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetFromJsonAsync<List<ClientDto>>("/clients", cancellationToken);
            return result ?? new List<ClientDto>();
        }

        /// <summary>Registers a new client in the catalog.</summary>
        public async Task<ClientDto> CreateClientAsync(CreateClientRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/clients", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ClientDto>(cancellationToken))!;
        }

        /// <summary>Retrieves all routes registered in the catalog.</summary>
        public async Task<IReadOnlyList<RouteDto>> GetRoutesAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetFromJsonAsync<List<RouteDto>>("/routes", cancellationToken);
            return result ?? new List<RouteDto>();
        }

        /// <summary>Registers a new route in the catalog.</summary>
        public async Task<RouteDto> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/routes", request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new HttpRequestException(await ReadBadRequestMessageAsync(response, cancellationToken), null, response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<RouteDto>(cancellationToken))!;
        }

        // Catalog.Api returns its validation messages as a JSON string body (Results.BadRequest(string)).
        // Other 400s, e.g. a request body the framework couldn't bind, carry no such message.
        private static async Task<string> ReadBadRequestMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.StartsWith('"')
                ? JsonSerializer.Deserialize<string>(body) ?? body
                : "The request was rejected by the Catalog API (400 Bad Request).";
        }
    }
}
