using System.Security.Cryptography;
using System.Text;

namespace DestinationPlanner.Helpers;

// Persists the Navigraph OAuth refresh token, encrypted at rest via Windows DPAPI
// (current-user scope). Exceptions are swallowed the same way AppSettingsService
// treats settings as always best-effort, never fatal — a decrypt failure (wrong
// user profile, corruption) is simply treated as "not signed in."
public static class NavigraphTokenStore
{
    public static void Save(AppSettings settings, string refreshToken)
    {
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser);
            settings.NavigraphRefreshTokenProtected = Convert.ToBase64String(protectedBytes);
            AppSettingsService.Save(settings);
        }
        catch { }
    }

    public static string? TryLoad(AppSettings settings)
    {
        if (settings.NavigraphRefreshTokenProtected is not { } base64) return null;

        try
        {
            byte[] bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(base64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(AppSettings settings)
    {
        settings.NavigraphRefreshTokenProtected = null;
        AppSettingsService.Save(settings);
    }
}
