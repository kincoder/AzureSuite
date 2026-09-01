using AzureSuite.Domain.Entities;
using AzureSuite.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Infrastructure.Tests.Persistence;

public class AppDbContextTests
{
    // Each test gets its own uniquely-named in-memory database so tests can run
    // in parallel without sharing state.
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Email_HasUniqueIndex()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(User))!;
        var index = entityType.GetIndexes().Single(i => i.Properties.Single().Name == nameof(User.Email));

        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Email_HasMaxLengthOf256()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(User))!;
        var emailProperty = entityType.FindProperty(nameof(User.Email))!;

        emailProperty.GetMaxLength().Should().Be(256);
    }

    [Fact]
    public async Task Users_CanBeAddedAndRetrieved()
    {
        using var context = CreateContext();
        var user = new User { Email = "test@example.com", PasswordHash = "hash" };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FindAsync(user.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("test@example.com");
    }
}
