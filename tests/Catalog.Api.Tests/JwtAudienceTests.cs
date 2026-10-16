using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Catalog.Api.Tests
{
    /// <summary>Checks the real JWT bearer configuration (the other tests swap it for
    /// TestAuthHandler): with requestedAccessTokenVersion 2, Entra sets <c>aud</c> to the API's
    /// client id, so that is what audience validation must accept.</summary>
    public class JwtAudienceTests : IClassFixture<CatalogApiFactory>
    {
        private readonly TokenValidationParameters _parameters;

        public JwtAudienceTests(CatalogApiFactory factory)
        {
            _parameters = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme)
                .TokenValidationParameters;
        }

        private static JsonWebToken V2TokenFor(string audience)
        {
            static string Encode(string s) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(s));
            return new JsonWebToken($"{Encode("{\"alg\":\"none\"}")}.{Encode($"{{\"aud\":\"{audience}\",\"ver\":\"2.0\"}}")}.");
        }

        private bool Accepts(string audience)
        {
            var token = V2TokenFor(audience);
            try
            {
                if (_parameters.AudienceValidator is { } validator)
                {
                    return validator([audience], token, _parameters);
                }

                Validators.ValidateAudience([audience], token, _parameters);
                return true;
            }
            catch (SecurityTokenException)
            {
                return false;
            }
        }

        [Fact]
        public void AcceptsAV2TokenWhoseAudienceIsTheApiClientId()
        {
            Accepts(CatalogApiFactory.TestApiClientId).Should().BeTrue();
        }

        [Fact]
        public void RejectsATokenForAnotherAudience()
        {
            Accepts("00000003-0000-0000-c000-000000000000").Should().BeFalse();
        }
    }
}
