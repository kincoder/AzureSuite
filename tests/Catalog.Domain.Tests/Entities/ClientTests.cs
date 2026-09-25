using AzureSuite.Catalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities
{
    public class ClientTests
    {
        [Fact]
        public void Constructor_WithValidName_SetsProperties()
        {
            var client = new Client("Acme Corp");

            client.Id.Should().NotBeEmpty();
            client.Name.Should().Be("Acme Corp");
            client.RegisteredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithMissingName_ThrowsArgumentException(string? name)
        {
            var act = () => new Client(name!);

            act.Should().Throw<ArgumentException>();
        }
    }
}
