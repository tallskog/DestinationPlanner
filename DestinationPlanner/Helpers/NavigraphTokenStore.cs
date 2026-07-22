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
            settings.NavigraphRefreshTokenProtected = Protect(refreshToken);
            AppSettingsService.Save(settings);
        }
        catch { }
    }

    public static string? TryLoad(AppSettings settings)
    {
        if (settings.NavigraphRefreshTokenProtected is not { } base64) return null;
        return Unprotect(base64);
    }

    public static void Clear(AppSettings settings)
    {
        settings.NavigraphRefreshTokenProtected = null;
        AppSettingsService.Save(settings);
    }

    // Extracted so the DPAPI round-trip can be unit tested without touching settings.json.
    internal static string Protect(string plainText)
    {
        byte[] protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    internal static string? Unprotect(string protectedBase64)
    {
        try
        {
            byte[] bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedBase64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
