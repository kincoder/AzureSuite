using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities
{
    public class MessageTypeTests
    {
        [Fact]
        public void Constructor_WithValidArguments_SetsProperties()
        {
            var name = new MessageTypeName("pacs.008");
            var version = new MessageTypeVersion("1.0");

            var messageType = new MessageType(name, version, "{ \"type\": \"object\" }");

            messageType.Name.Should().Be(name);
            messageType.Version.Should().Be(version);
            messageType.SchemaDefinition.Should().Be("{ \"type\": \"object\" }");
            messageType.Id.Should().NotBeEmpty();
            messageType.RegisteredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Constructor_WithNullName_ThrowsArgumentNullException()
        {
            var act = () => new MessageType(null!, new MessageTypeVersion("1.0"), "{}");

            act.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }

        [Fact]
        public void Constructor_WithNullVersion_ThrowsArgumentNullException()
        {
            var act = () => new MessageType(new MessageTypeName("pacs.008"), null!, "{}");

            act.Should().Throw<ArgumentNullException>().WithParameterName("version");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithInvalidSchemaDefinition_ThrowsArgumentException(string? invalidSchema)
        {
            var act = () => new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), invalidSchema!);

            act.Should().Throw<ArgumentException>().WithParameterName("schemaDefinition");
        }
    }
}
