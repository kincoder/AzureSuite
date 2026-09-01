namespace AzureSuite.Domain.Entities;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
