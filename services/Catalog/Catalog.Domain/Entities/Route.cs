using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Domain.Entities
{
    /// <summary>Authorizes a Client to send a given MessageType/Version, and says where it goes.
    /// No Route for a (ClientId, MessageTypeName, MessageTypeVersion) triple means that client
    /// is not authorized to send that message type.</summary>
    public class Route
    {
        public Guid Id { get; }

        public Guid ClientId { get; }

        public MessageTypeName MessageTypeName { get; }

        public MessageTypeVersion MessageTypeVersion { get; }

        /// <summary>One or more Service Bus output queue names a matching message is fanned out to.</summary>
        public IReadOnlyList<string> QueueNames { get; }

        public Route(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IReadOnlyList<string> queueNames)
        {
            // EF Core's constructor-binding for primitive collections at design time requires
            // the constructor parameter's type to match the mapped property's type exactly
            // (IReadOnlyList<string>, not IEnumerable<string>) -- otherwise migrations add fails.
            if (queueNames.Count == 0)
            {
                throw new ArgumentException("At least one queue name is required.", nameof(queueNames));
            }

            Id = Guid.NewGuid();
            ClientId = clientId;
            MessageTypeName = messageTypeName;
            MessageTypeVersion = messageTypeVersion;
            QueueNames = queueNames.ToList();
        }
    }
}
