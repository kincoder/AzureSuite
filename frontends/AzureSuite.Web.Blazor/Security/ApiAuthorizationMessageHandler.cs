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
    }
}
