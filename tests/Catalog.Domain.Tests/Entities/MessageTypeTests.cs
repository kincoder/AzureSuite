using AzureSuite.Catalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities;

public class MessageTypeTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var messageType = new MessageType("pacs.008", "1.0", "{ \"type\": \"object\" }");

        messageType.Name.Should().Be("pacs.008");
        messageType.Version.Should().Be("1.0");
        messageType.SchemaDefinition.Should().Be("{ \"type\": \"object\" }");
        messageType.Id.Should().NotBeEmpty();
        messageType.RegisteredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        var act = () => new MessageType(invalidName!, "1.0", "{}");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidVersion_ThrowsArgumentException(string? invalidVersion)
    {
        var act = () => new MessageType("pacs.008", invalidVersion!, "{}");

        act.Should().Throw<ArgumentException>().WithParameterName("version");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidSchemaDefinition_ThrowsArgumentException(string? invalidSchema)
    {
        var act = () => new MessageType("pacs.008", "1.0", invalidSchema!);

        act.Should().Throw<ArgumentException>().WithParameterName("schemaDefinition");
    }
}
