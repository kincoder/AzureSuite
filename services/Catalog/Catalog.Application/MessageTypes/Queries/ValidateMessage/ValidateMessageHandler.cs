using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.ValueObjects;
using Json.Schema;
using MediatR;
using System.Text.Json;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    public class ValidateMessageHandler : IRequestHandler<ValidateMessageQuery, ValidationResultDto>
    {
        // Payload is untrusted external input (ultimately producer-submitted). JsonDocument.Parse
        // has no size limit of its own, so an oversized payload would allocate and parse fully
        // before validation ever runs -- a denial-of-service vector. Reject before parsing.
        private const int MaxPayloadSizeBytes = 1_000_000;

        private readonly IMessageTypeRepository _repository;

        public ValidateMessageHandler(IMessageTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<ValidationResultDto> Handle(ValidateMessageQuery request, CancellationToken cancellationToken)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(request.Payload) > MaxPayloadSizeBytes)
            {
                return new ValidationResultDto(false, new[] { $"Payload exceeds the maximum allowed size of {MaxPayloadSizeBytes:N0} bytes." });
            }

            var messageType = await _repository.GetByNameAndVersionAsync(
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                cancellationToken);

            if (messageType is null)
            {
                return new ValidationResultDto(false, new[] { $"Message type '{request.MessageTypeName}' version '{request.MessageTypeVersion}' is not registered." });
            }

            var schema = JsonSchema.FromText(messageType.SchemaDefinition);
            using var payloadDocument = JsonDocument.Parse(request.Payload);
            var evaluationResult = schema.Evaluate(payloadDocument.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

            if (evaluationResult.IsValid)
            {
                return new ValidationResultDto(true, Array.Empty<string>());
            }

            var details = evaluationResult.Details ?? new List<EvaluationResults>();
            var errors = details
                .Where(d => !d.IsValid && d.Errors is not null)
                .SelectMany(d => d.Errors!.Values)
                .ToList();

            return new ValidationResultDto(false, errors);
        }
    }
}
