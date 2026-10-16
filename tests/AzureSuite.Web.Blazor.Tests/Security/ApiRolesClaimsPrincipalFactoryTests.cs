using AzureSuite.Web.Blazor.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace AzureSuite.Web.Blazor.Tests.Security
{
    public class ApiRolesClaimsPrincipalFactoryTests
    {
        private sealed class FakeTokenProvider : IAccessTokenProvider, IAccessTokenProviderAccessor
        {
            private readonly string? _token;
            public FakeTokenProvider(string? token) => _token = token;
            public IAccessTokenProvider TokenProvider => this;
            public string[] LastRequestedScopes { get; private set; } = [];

            public ValueTask<AccessTokenResult> RequestAccessToken() => RequestAccessToken(new AccessTokenRequestOptions());

            public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
            {
                LastRequestedScopes = options.Scopes?.ToArray() ?? [];
                var status = _token is null ? AccessTokenResultStatus.RequiresRedirect : AccessTokenResultStatus.Success;
                var token = new AccessToken { Value = _token ?? string.Empty, Expires = DateTimeOffset.UtcNow.AddHours(1), GrantedScopes = LastRequestedScopes };
                return ValueTask.FromResult(new AccessTokenResult(status, token, "authentication/login", null));
            }
        }

        private static readonly ApiAccessOptions Api = new("api://tenant/messaginghub-api/access_as_user", []);
        private static readonly RemoteAuthenticationUserOptions UserOptions = new() { AuthenticationType = "msal", NameClaim = "name" };
        private static RemoteUserAccount SignedInAccount() => new() { AdditionalProperties = new Dictionary<string, object> { ["name"] = "Oscar" } };

        [Fact]
        public async Task CreateUserAsync_AddsTheRolesFromTheApiAccessToken()
        {
            var provider = new FakeTokenProvider(JwtRolesTests.TokenWithPayload("{\"roles\":[\"Catalog.Admin\",\"Messages.Submit\"]}"));
            var factory = new ApiRolesClaimsPrincipalFactory(provider, Api);

            var user = await factory.CreateUserAsync(SignedInAccount(), UserOptions);

            user.IsInRole("Catalog.Admin").Should().BeTrue();
            user.IsInRole("Messages.Submit").Should().BeTrue();
            user.IsInRole("Catalog.Reader").Should().BeFalse();
            provider.LastRequestedScopes.Should().Equal(Api.Scope);
        }

        [Fact]
        public async Task CreateUserAsync_WhenNoTokenIsAvailable_ReturnsTheUserWithoutRoles()
        {
            var factory = new ApiRolesClaimsPrincipalFactory(new FakeTokenProvider(null), Api);

            var user = await factory.CreateUserAsync(SignedInAccount(), UserOptions);

            user.Identity!.IsAuthenticated.Should().BeTrue();
            user.IsInRole("Catalog.Admin").Should().BeFalse();
        }

        [Fact]
        public async Task CreateUserAsync_ForASignedOutAccount_DoesNotRequestAToken()
        {
            var provider = new FakeTokenProvider("unused");
            var factory = new ApiRolesClaimsPrincipalFactory(provider, Api);

            var user = await factory.CreateUserAsync(null!, UserOptions);

            user.Identity!.IsAuthenticated.Should().BeFalse();
            provider.LastRequestedScopes.Should().BeEmpty();
        }
    }
}
