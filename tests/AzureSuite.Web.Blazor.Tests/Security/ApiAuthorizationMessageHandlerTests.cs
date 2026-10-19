using System.Net;
using AzureSuite.Web.Blazor.Security;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Security
{
    public class ApiAuthorizationMessageHandlerTests : BunitContext
    {
        private sealed class InteractionRequiredProvider : IAccessTokenProvider
        {
            public ValueTask<AccessTokenResult> RequestAccessToken() => RequestAccessToken(new AccessTokenRequestOptions());

            // Like MSAL: a redirect-required result carries the interaction options for sign-in.
            public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options) =>
                ValueTask.FromResult(new AccessTokenResult(
                    AccessTokenResultStatus.RequiresRedirect,
                    new AccessToken(),
                    "authentication/login",
                    new InteractiveRequestOptions { Interaction = InteractionType.GetToken, ReturnUrl = "/catalog/clients" }));
        }

        private sealed class OkHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        [Fact]
        public async Task WhenTheTokenNeedsInteraction_SendsTheUserToSignIn()
        {
            var navigation = Services.GetRequiredService<NavigationManager>();
            var handler = new ApiAuthorizationMessageHandler(
                new InteractionRequiredProvider(), navigation, new ApiAccessOptions("api://scope", [new Uri("https://api.test/")]))
            {
                InnerHandler = new OkHandler()
            };
            using var client = new HttpClient(handler);

            var send = () => client.GetAsync("https://api.test/clients");

            await send.Should().ThrowAsync<AccessTokenNotAvailableException>();
            navigation.Uri.Should().Contain("authentication/login");
        }
    }
}
