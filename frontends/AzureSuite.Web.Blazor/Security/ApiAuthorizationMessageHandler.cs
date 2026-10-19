using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>Attaches an API access token to requests sent to <see cref="ApiAccessOptions.AuthorizedUrls"/>.</summary>
    public sealed class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
    {
        public ApiAuthorizationMessageHandler(IAccessTokenProvider provider, NavigationManager navigation, ApiAccessOptions api)
            : base(provider, navigation)
        {
            ConfigureHandler(authorizedUrls: api.AuthorizedUrls.Select(u => u.ToString()), scopes: [api.Scope]);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (AccessTokenNotAvailableException ex)
            {
                // The token can't be renewed silently (e.g. the refresh token expired): send the user
                // to sign-in here, once, instead of leaving every page to handle it. The exception
                // still propagates so the calling page stops what it was doing.
                ex.Redirect();
                throw;
            }
        }
    }
}
