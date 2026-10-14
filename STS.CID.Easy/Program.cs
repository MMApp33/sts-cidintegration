namespace STS.CID.Easy;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\ScanToServe.CidEasy", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("ScanToServe CID Easy is already running (check the system tray).",
                "ScanToServe CID Easy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Error("Unhandled UI exception.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("Unhandled exception.", e.ExceptionObject as Exception);

        Log.Info($"Application start v{LocalApiServer.Version}");

        var settings = AppSettings.Load();
        WindowsStartup.SetEnabled(settings.StartWithWindows);

        var store = new CallStore();
        var server = new LocalApiServer(store, settings);
        string? startError = null;
        try
        {
            server.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            startError = ex.Message;
            Log.Error($"Local API failed to start on {server.BaseUrl}.", ex);
        }

        var startMinimized = settings.StartMinimized || args.Contains("--minimized");
        Application.Run(new MainForm(settings, store, server, startMinimized, startError));

        server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Log.Info("Application exit");
    }
}
