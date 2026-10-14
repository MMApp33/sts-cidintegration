using System.Diagnostics;
using Microsoft.Win32;

namespace STS.CID.Easy;

/// <summary>
/// Reads and writes the CID Easy "Easy Host" settings that make it forward caller ID to a URL.
/// See https://cideasy.com/web-development-206.
/// </summary>
public static class EasyHost
{
    private const string VendorKeyPath = @"SOFTWARE\Kybele Software\CID Easy";
    private const string UrlValueName = "Web Connection URL/Post Suffix";
    private const string RegistrationKeyValueName = "Registration Key";

    /// <summary>Easy Host only substitutes these exact placeholder names.</summary>
    public static string BuildForwardUrl(int port) =>
        $"http://127.0.0.1:{port}/callerid?caller=<CallerNumber>&devserial=<DeviceSerial>&port=<InputPort>&date=<Year><Month><Day>&time=<Hour><Minute>";

    public static bool IsRunning()
    {
        try
        {
            return Process.GetProcesses().Any(p =>
            {
                var name = p.ProcessName.Replace(" ", string.Empty);
                return name.Contains("EasyHost", StringComparison.OrdinalIgnoreCase);
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Easy Host writes its key as "Easy Host X.X" on first run, so the version suffix is unknown in advance.
    /// </summary>
    public static string? FindHostKeyName()
    {
        using var vendor = Registry.CurrentUser.OpenSubKey(VendorKeyPath);
        return vendor?
            .GetSubKeyNames()
            .Where(n => n.StartsWith("Easy Host", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static string? ReadForwardUrl()
    {
        var hostKey = FindHostKeyName();
        if (hostKey is null)
        {
            return null;
        }

        using var key = Registry.CurrentUser.OpenSubKey($@"{VendorKeyPath}\{hostKey}");
        return key?.GetValue(UrlValueName) as string;
    }

    public static bool IsConfiguredFor(int port) =>
        string.Equals(ReadForwardUrl(), BuildForwardUrl(port), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Points Easy Host at this app and activates its POST option.
    /// Easy Host must be restarted afterwards to pick up the change.
    /// </summary>
    public static ConfigureResult Configure(int port)
    {
        var hostKey = FindHostKeyName();
        if (hostKey is null)
        {
            return new ConfigureResult(false,
                "Easy Host settings were not found in the registry.\n\n" +
                "Install Easy Host and run it once manually, then try again.");
        }

        using var key = Registry.CurrentUser.OpenSubKey($@"{VendorKeyPath}\{hostKey}", writable: true);
        if (key is null)
        {
            return new ConfigureResult(false, $"Could not open registry key '{hostKey}' for writing.");
        }

        var url = BuildForwardUrl(port);
        key.SetValue(UrlValueName, url, RegistryValueKind.String);
        Log.Info($"Easy Host ({hostKey}) forward URL set to {url}");

        var note = string.Empty;
        if (key.GetValue(RegistrationKeyValueName) is string registration &&
            registration.StartsWith("--", StringComparison.Ordinal))
        {
            key.SetValue(RegistrationKeyValueName, registration[2..], RegistryValueKind.String);
            Log.Info("Removed leading '--' from Easy Host Registration Key to enable web POST.");
            note = "\n\nThe web POST option was also enabled.";
        }

        return new ConfigureResult(true,
            $"Easy Host is now set to forward calls to this app.{note}\n\n" +
            "Restart Easy Host (or the PC) for the change to take effect.");
    }

    public sealed record ConfigureResult(bool Success, string Message);
}
