using AzureSuite.Processing.Application.Messages.Commands.RunHousekeeping;
using AzureSuite.Processing.Functions;
using AzureSuite.Processing.Functions.Functions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Processing.Functions.Tests.TestDoubles;

namespace Processing.Functions.Tests.Functions
{
    public class RunHousekeepingFunctionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

        private static RunHousekeepingFunction NewFunction(FakeSender sender, HousekeepingOptions options) =>
            new(sender, new FakeTimeProvider(Now), Options.Create(options), NullLogger<RunHousekeepingFunction>.Instance);

        [Fact]
        public async Task Run_SendsHousekeepingWithTheCurrentTimeAndConfiguredLimits()
        {
            var sender = new FakeSender { ResponseToReturn = new HousekeepingReport(0, 0, Now.UtcDateTime) };

            await NewFunction(sender, new HousekeepingOptions { RetentionDays = 7, MaxDeadLettersPerRun = 50 })
                .Run(new TimerInfo(), CancellationToken.None);

            sender.Sent.Should().ContainSingle().Which.Should().Be(new RunHousekeepingCommand(Now.UtcDateTime, 7, 50));
        }

        [Fact]
        public async Task Run_ReturnsTheReport()
        {
            var report = new HousekeepingReport(3, 12, Now.UtcDateTime.AddDays(-30));
            var sender = new FakeSender { ResponseToReturn = report };

            var result = await NewFunction(sender, new HousekeepingOptions()).Run(new TimerInfo { IsPastDue = true }, CancellationToken.None);

            result.Should().Be(report);
        }
    }
}
