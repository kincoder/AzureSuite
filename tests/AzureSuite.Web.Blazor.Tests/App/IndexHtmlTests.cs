using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.App
{
    /// <summary>Checks wwwroot/index.html, which no component test renders.</summary>
    public class IndexHtmlTests
    {
        private static string ReadIndexHtml()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AzureSuite.slnx")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the tests run inside the repository");
            return File.ReadAllText(Path.Combine(directory!.FullName, "frontends", "AzureSuite.Web.Blazor", "wwwroot", "index.html"));
        }

        [Fact]
        public void LoadsTheMsalAuthenticationScriptBeforeBlazorStarts()
        {
            // AddMsalAuthentication calls AuthenticationService.init in JavaScript; without this
            // script every page fails with "'AuthenticationService' was undefined".
            var html = ReadIndexHtml();
            const string msalScript = "_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js";

            html.Should().Contain(msalScript);
            html.IndexOf(msalScript, StringComparison.Ordinal).Should()
                .BeLessThan(html.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal));
        }
    }
}
