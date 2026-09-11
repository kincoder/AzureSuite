namespace AzureSuite.Catalog.Domain.ValueObjects
{
    /// <summary>
    /// The schema version of a registered message type, e.g. "1.0". Distinct versions of the
    /// same <see cref="MessageTypeName"/> are separate catalog entries, allowing producers/
    /// subscribers on different schema revisions to coexist.
    /// </summary>
    public sealed record MessageTypeVersion
    {
        /// <summary>The raw version value, e.g. "1.0".</summary>
        public string Value { get; }

        public MessageTypeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Message type version is required.", nameof(value));
            }

            if (value.Length > 20)
            {
                throw new ArgumentException("Message type version must be 20 characters or fewer.", nameof(value));
            }

            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
