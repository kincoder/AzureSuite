using AzureSuite.Web.UI;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests;

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
