using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ingestion.Api.Tests
{
    /// <summary>Stands in for JWT bearer validation in integration tests. A request carrying
    /// <see cref="RolesHeader"/> is authenticated with those comma-separated roles (an empty value
    /// means signed in with no roles); a request without it is anonymous, so it gets a 401.</summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var roles = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
