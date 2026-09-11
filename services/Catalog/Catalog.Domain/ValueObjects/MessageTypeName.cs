namespace AzureSuite.Catalog.Domain.ValueObjects
{
    /// <summary>
    /// The unique name of a financial message type in the Catalog, e.g. "pacs.008" or "camt.054".
    /// Combined with <see cref="MessageTypeVersion"/>, identifies exactly one registered schema.
    /// </summary>
    public sealed record MessageTypeName
    {
        /// <summary>The raw name value, e.g. "pacs.008".</summary>
        public string Value { get; }

        public MessageTypeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Message type name is required.", nameof(value));
            }

            if (value.Length > 100)
            {
                throw new ArgumentException("Message type name must be 100 characters or fewer.", nameof(value));
            }

            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
