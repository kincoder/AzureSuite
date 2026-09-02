using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AzureSuite.Infrastructure.Configuration;

/// <summary>
/// Maps Key Vault secret names to configuration keys explicitly, following this
/// project's {resource}-{purpose} naming convention (e.g. "sql-admin-password")
/// instead of Key Vault's default "--" to ":" auto-mapping, which would force
/// awkward secret names just to satisfy .NET config-section nesting.
/// </summary>
internal class MappingKeyVaultSecretManager(IReadOnlyDictionary<string, string> secretNameToConfigKey) : KeyVaultSecretManager
{
    public override bool Load(SecretProperties secret) => secretNameToConfigKey.ContainsKey(secret.Name);

    public override string GetKey(KeyVaultSecret secret) => secretNameToConfigKey[secret.Name];
}

public static class KeyVaultConfigurationExtensions
{
    /// <summary>
    /// Adds Key Vault as a configuration source, mapping only the given secret names to
    /// the given configuration keys. In Development, uses the Azure CLI credential
    /// directly (your az login session) rather than the full DefaultAzureCredential
    /// chain, which otherwise wastes 10-20s probing Managed Identity's instance metadata
    /// endpoint - an endpoint that doesn't exist on a dev machine. Once actually deployed
    /// to Azure, the full chain (including Managed Identity) is used automatically.
    /// </summary>
    public static void AddAzureSuiteKeyVault(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment,
        Uri vaultUri,
        IReadOnlyDictionary<string, string> secretNameToConfigKey)
    {
        TokenCredential credential = environment.IsDevelopment()
            ? new AzureCliCredential()
            : new DefaultAzureCredential();

        configuration.AddAzureKeyVault(vaultUri, credential, new MappingKeyVaultSecretManager(secretNameToConfigKey));
    }
}
