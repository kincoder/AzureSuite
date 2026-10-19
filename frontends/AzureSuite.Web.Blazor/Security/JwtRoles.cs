using System.Text;
using System.Text.Json;

namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>Reads the <c>roles</c> claim from a JWT's payload without validating it. Use it
    /// only to decide what the UI shows; the APIs validate the same token and enforce the roles.</summary>
    public static class JwtRoles
    {
        public static IReadOnlyList<string> Read(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                return [];
            }

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

                return document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array
                    ? roles.EnumerateArray().Select(r => r.GetString()).OfType<string>().ToList()
                    : [];
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                return [];
            }
        }
    }
}
