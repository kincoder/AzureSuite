using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using AzureSuite.Web.Blazor.Features.Ingestion.Contracts;

namespace AzureSuite.Web.Blazor.Features.Ingestion.Services
{
    /// <summary>Thin wrapper over Ingestion.Api's HTTP endpoint.</summary>
    public class IngestionApiClient
    {
        private readonly HttpClient _httpClient;

        public IngestionApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>Submits a message for ingestion.</summary>
        public async Task<IngestedMessageDto> SubmitMessageAsync(IngestMessageRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/messages", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IngestedMessageDto>(cancellationToken))!;
        }
    }
}
