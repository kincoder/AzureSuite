namespace AzureSuite.Catalog.Domain.Entities
{
    /// <summary>A registered sender identity. A <see cref="Route"/> ties a Client to the
    /// message types it's authorized to send.</summary>
    public class Client
    {
        public Guid Id { get; }

        public string Name { get; }

        public DateTime RegisteredAtUtc { get; }

        public Client(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Client name is required.", nameof(name));
            }

            Id = Guid.NewGuid();
            Name = name;
            RegisteredAtUtc = DateTime.UtcNow;
        }
    }
}
