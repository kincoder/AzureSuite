using System.Diagnostics;
using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage
{
    public class IngestMessageCommandHandler : IRequestHandler<IngestMessageCommand, IngestedMessageDto>
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<IngestMessageCommandHandler> _logger;

        public IngestMessageCommandHandler(IMessagePublisher publisher, ILogger<IngestMessageCommandHandler> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<IngestedMessageDto> Handle(IngestMessageCommand request, CancellationToken cancellationToken)
        {
            var messageId = Guid.CreateVersion7();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _publisher.PublishAsync(messageId, request.MessageType, request.Version, request.Payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish message {MessageId} of type {MessageType} v{Version}",
                    messageId, request.MessageType, request.Version);
                throw;
            }

            _logger.LogInformation(
                "Published message {MessageId} of type {MessageType} v{Version} in {ElapsedMilliseconds}ms",
                messageId, request.MessageType, request.Version, stopwatch.ElapsedMilliseconds);

            return new IngestedMessageDto(messageId, request.MessageType, request.Version);
        }
    }
}
