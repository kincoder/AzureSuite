namespace AzureSuite.Catalog.Domain.ValueObjects;

public sealed record MessageTypeVersion
{
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

    public override string ToString() => Value;
}
