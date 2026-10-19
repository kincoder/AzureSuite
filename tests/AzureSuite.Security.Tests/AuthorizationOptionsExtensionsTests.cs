using System.Security.Claims;
using AzureSuite.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Security.Tests
{
    public class AuthorizationOptionsExtensionsTests
    {
        private readonly IAuthorizationService _authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(options => options.AddAzureSuitePolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        private static ClaimsPrincipal SignedInWith(params string[] roles) =>
            new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), authenticationType: "Test"));

        private async Task<bool> Allows(ClaimsPrincipal user, string policy) =>
            (await _authorization.AuthorizeAsync(user, policy)).Succeeded;

        [Theory]
        [InlineData(AppRoles.CatalogReader, true)]
        [InlineData(AppRoles.CatalogAdmin, true)]
        [InlineData(AppRoles.MessagesSubmit, false)]
        public async Task CatalogRead_AllowsReaderOrAdminOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.CatalogRead)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppRoles.CatalogAdmin, true)]
        [InlineData(AppRoles.CatalogReader, false)]
        [InlineData(AppRoles.MessagesSubmit, false)]
        public async Task CatalogWrite_AllowsAdminOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.CatalogWrite)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppRoles.MessagesSubmit, true)]
        [InlineData(AppRoles.CatalogAdmin, false)]
        public async Task SubmitMessages_AllowsSubmitterOnly(string role, bool expected)
        {
            (await Allows(SignedInWith(role), AppPolicies.SubmitMessages)).Should().Be(expected);
        }

        [Theory]
        [InlineData(AppPolicies.CatalogRead)]
        [InlineData(AppPolicies.CatalogWrite)]
        [InlineData(AppPolicies.SubmitMessages)]
        public async Task EveryPolicy_DeniesASignedInUserWithNoRoles(string policy)
        {
            (await Allows(SignedInWith(), policy)).Should().BeFalse();
        }

        [Theory]
        [InlineData(AppPolicies.CatalogRead, AppRoles.CatalogAdmin)]
        [InlineData(AppPolicies.CatalogWrite, AppRoles.CatalogAdmin)]
        [InlineData(AppPolicies.SubmitMessages, AppRoles.MessagesSubmit)]
        public async Task EveryPolicy_DeniesAnUnauthenticatedIdentityEvenWithTheRoleClaim(string policy, string role)
        {
            var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)]));

            (await Allows(unauthenticated, policy)).Should().BeFalse();
        }
    }
}
