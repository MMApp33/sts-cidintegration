using Microsoft.Win32;

namespace STS.CID.Easy;

public static class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScanToServe CID Easy";

    public static bool IsEnabled() =>
        HasValue(Registry.LocalMachine) || HasValue(Registry.CurrentUser);

    public static void SetEnabled(bool enabled)
    {
        // Installer writes HKLM (Program Files / all users). Prefer that so we don't
        // also write HKCU and start twice at login (mutex would show "already running").
        if (TryApply(Registry.LocalMachine, enabled))
        {
            TryDelete(Registry.CurrentUser);
            return;
        }

        if (!TryApply(Registry.CurrentUser, enabled))
        {
            Log.Error("Failed to update Start with Windows (no permission for Run key).");
        }
    }

    private static bool HasValue(RegistryKey hive)
    {
        try
        {
            using var key = hive.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryApply(RegistryKey hive, bool enabled)
    {
        try
        {
            using var key = hive.OpenSubKey(RunKeyPath, writable: true)
                            ?? (enabled ? hive.CreateSubKey(RunKeyPath) : null);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to update Start with Windows ({hive.Name}).", ex);
            return false;
        }
    }

    private static void TryDelete(RegistryKey hive)
    {
        try
        {
            using var key = hive.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort cleanup of a duplicate user entry.
        }
    }
}
