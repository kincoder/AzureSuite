using AzureSuite.Ingestion.Application.Messages;
using MediatR;

namespace AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage
{
    /// <summary>Accepts a raw message for publishing. No validation beyond "these fields are
    /// present" happens here or in its handler — see the Ingestion design spec for why identification,
    /// structural, and semantic validation are all deliberately deferred to a future downstream stage.</summary>
    /// <param name="MessageType">Declared message type, e.g. "pacs.008".</param>
    /// <param name="Version">Declared schema version, e.g. "1.0".</param>
    /// <param name="Payload">The raw message payload as submitted.</param>
    public record IngestMessageCommand(string MessageType, string Version, string Payload) : IRequest<IngestedMessageDto>;
}
