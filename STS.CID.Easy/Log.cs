namespace STS.CID.Easy;

/// <summary>
/// Minimal daily rolling file log in a Logs folder beside the executable.
/// </summary>
public static class Log
{
    private static readonly object Sync = new();

    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "Logs");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        lock (Sync)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(
                    Path.Combine(Directory, $"CidEasy-{DateTime.Now:yyyy-MM-dd}.log"),
                    line + Environment.NewLine);
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }
}
