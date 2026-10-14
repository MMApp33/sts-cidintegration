using System.Text.Json;

namespace STS.CID.Easy;

/// <summary>
/// Settings persisted to settings.json beside the executable.
/// </summary>
public sealed class AppSettings
{
    public int ApiPort { get; set; } = 5050;

    public bool StartWithWindows { get; set; } = true;

    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// Browser origins allowed to call the local API (the ScanToServe admin site).
    /// Entries may use a leading "*." subdomain wildcard, e.g. https://*.scantoserve.com.
    /// </summary>
    public string[] AllowedOrigins { get; set; } =
    [
        "https://scantoserve.com",
        "https://*.scantoserve.com",
        "http://localhost:3000"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                {
                    if (loaded.ApiPort is < 1 or > 65535)
                    {
                        loaded.ApiPort = 5050;
                    }

                    loaded.AllowedOrigins ??= [];
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to read settings.json; using defaults.", ex);
        }

        var defaults = new AppSettings();
        defaults.Save();
        return defaults;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write settings.json.", ex);
        }
    }
}
