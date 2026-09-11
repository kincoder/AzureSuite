using AzureSuite.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.ValueObjects
{
    public class MessageTypeNameTests
    {
        [Fact]
        public void Constructor_WithValidValue_SetsValue()
        {
            var name = new MessageTypeName("pacs.008");

            name.Value.Should().Be("pacs.008");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithBlankValue_ThrowsArgumentException(string? invalidValue)
        {
            var act = () => new MessageTypeName(invalidValue!);

            act.Should().Throw<ArgumentException>().WithParameterName("value");
        }

        [Fact]
        public void Constructor_WithValueLongerThan100Characters_ThrowsArgumentException()
        {
            var tooLong = new string('a', 101);

            var act = () => new MessageTypeName(tooLong);

            act.Should().Throw<ArgumentException>().WithParameterName("value");
        }

        [Fact]
        public void TwoInstances_WithTheSameValue_AreEqual()
        {
            var first = new MessageTypeName("pacs.008");
            var second = new MessageTypeName("pacs.008");

            first.Should().Be(second);
        }

        [Fact]
        public void ToString_ReturnsTheUnderlyingValue()
        {
            var name = new MessageTypeName("pacs.008");

            name.ToString().Should().Be("pacs.008");
        }
    }
}
