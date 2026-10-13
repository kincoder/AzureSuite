using AzureSuite.Processing.Functions;
using FluentAssertions;

namespace Processing.Functions.Tests
{
    public class HousekeepingOptionsTests
    {
        [Fact]
        public void Defaults_KeepThirtyDaysAndCapEachRunAtFiveHundredDeadLetters()
        {
            var options = new HousekeepingOptions();

            options.RetentionDays.Should().Be(30);
            options.MaxDeadLettersPerRun.Should().Be(500);
        }
    }
}
