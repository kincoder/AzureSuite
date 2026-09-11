namespace AzureSuite.Catalog.Domain.ValueObjects;

public sealed record MessageTypeName
{
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

    public override string ToString() => Value;
}
