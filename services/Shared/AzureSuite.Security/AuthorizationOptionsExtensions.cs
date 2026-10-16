using Microsoft.AspNetCore.Authorization;

namespace AzureSuite.Security
{
    /// <summary>Registers the <see cref="AppPolicies"/> on an <see cref="AuthorizationOptions"/>.</summary>
    public static class AuthorizationOptionsExtensions
    {
        public static void AddAzureSuitePolicies(this AuthorizationOptions options)
        {
            // RequireRole alone only checks IsInRole, which an unauthenticated identity carrying a
            // role claim would pass; requiring authentication too closes that gap.
            options.AddPolicy(AppPolicies.CatalogRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.CatalogReader, AppRoles.CatalogAdmin));

            options.AddPolicy(AppPolicies.CatalogWrite, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.CatalogAdmin));

            options.AddPolicy(AppPolicies.SubmitMessages, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AppRoles.MessagesSubmit));
        }
    }
}
