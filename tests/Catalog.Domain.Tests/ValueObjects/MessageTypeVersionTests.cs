using AzureSuite.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.ValueObjects
{
    public class MessageTypeVersionTests
    {
        [Fact]
        public void Constructor_WithValidValue_SetsValue()
        {
            var version = new MessageTypeVersion("1.0");

            version.Value.Should().Be("1.0");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithBlankValue_ThrowsArgumentException(string? invalidValue)
        {
            var act = () => new MessageTypeVersion(invalidValue!);

            act.Should().Throw<ArgumentException>().WithParameterName("value");
        }

        [Fact]
        public void Constructor_WithValueLongerThan20Characters_ThrowsArgumentException()
        {
            var tooLong = new string('1', 21);

            var act = () => new MessageTypeVersion(tooLong);

            act.Should().Throw<ArgumentException>().WithParameterName("value");
        }

        [Fact]
        public void TwoInstances_WithTheSameValue_AreEqual()
        {
            var first = new MessageTypeVersion("1.0");
            var second = new MessageTypeVersion("1.0");

            first.Should().Be(second);
        }

        [Fact]
        public void ToString_ReturnsTheUnderlyingValue()
        {
            var version = new MessageTypeVersion("1.0");

            version.ToString().Should().Be("1.0");
        }
    }
}
