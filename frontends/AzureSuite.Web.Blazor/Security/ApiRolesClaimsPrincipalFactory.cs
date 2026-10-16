using System.Security.Claims;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>App roles live on the API registration, so they're in the API access token, not in
    /// this app's ID token. This copies them onto the signed-in user so AuthorizeView and
    /// [Authorize(Policy)] work, using the same role and policy definitions as the APIs.</summary>
    public sealed class ApiRolesClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
    {
        private readonly ApiAccessOptions _api;

        public ApiRolesClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor, ApiAccessOptions api)
            : base(accessor)
        {
            _api = api;
        }

        public override async ValueTask<ClaimsPrincipal> CreateUserAsync(RemoteUserAccount account, RemoteAuthenticationUserOptions options)
        {
            var user = await base.CreateUserAsync(account, options);
            if (user.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
            {
                return user;
            }

            var result = await TokenProvider.RequestAccessToken(new AccessTokenRequestOptions { Scopes = [_api.Scope] });
            if (result.TryGetToken(out var token))
            {
                foreach (var role in JwtRoles.Read(token.Value))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
                }
            }

            return user;
        }
    }
}
