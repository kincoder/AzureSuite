using AzureSuite.Web.Blazor.UI;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.UI;

public class ClientTelemetryLoggerTests : BunitContext
{
    [Fact]
    public async Task LogExceptionAsyncInvokesLogExceptionWithMessageAndExceptionTypeName()
    {
        var invocation = JSInterop.SetupVoid("logException", "boom", "InvalidOperationException");
        invocation.SetVoidResult();
        var logger = new ClientTelemetryLogger(JSInterop.JSRuntime);

        await logger.LogExceptionAsync(new InvalidOperationException("boom"));

        invocation.VerifyInvoke("logException");
    }
}
