using AzureSuite.Web.Blazor.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.UI.Components;

public class ErrorStateTests : BunitContext
{
    [Fact]
    public void RendersMessageInsideErrorStateDiv()
    {
        var cut = Render<ErrorState>(parameters => parameters
            .Add(p => p.Message, "Failed to load message types."));

        cut.Find(".error-state").TextContent.Should().Be("Failed to load message types.");
    }
}
