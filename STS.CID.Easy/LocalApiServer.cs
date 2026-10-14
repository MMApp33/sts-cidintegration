using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace STS.CID.Easy;

/// <summary>
/// Kestrel minimal API on 127.0.0.1 that
/// receives caller ID from Easy Host (/callerid) and serves it to the ScanToServe website
/// (/api/status, /api/latest-call) using the same contract as the router connector.
/// </summary>
public sealed class LocalApiServer : IAsyncDisposable
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);

    private readonly CallStore _store;
    private readonly AppSettings _settings;
    private WebApplication? _app;
    private IncomingCall? _lastAccepted;

    public LocalApiServer(CallStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public static string Version { get; } =
        (Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0")
        .Split('+')[0];

    public bool IsRunning => _app is not null;

    public string BaseUrl => $"http://127.0.0.1:{_settings.ApiPort}";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(BaseUrl);
        builder.Logging.ClearProviders();
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(_settings.AllowedOrigins)
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .AllowAnyMethod()));

        var app = builder.Build();

        // Chrome Private Network Access: a public HTTPS site calling 127.0.0.1 sends this on preflight.
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }

            await next();
        });
        app.UseCors();

        app.MapGet("/api/status", () => Results.Json(new
        {
            connected = EasyHost.IsRunning(),
            monitoring = true,
            version = Version
        }));

        app.MapGet("/api/latest-call", () =>
        {
            var call = _store.TakePending();
            return call is null
                ? Results.Json(new { hasNewCall = false })
                : Results.Json(new
                {
                    hasNewCall = true,
                    phoneNumber = call.PhoneNumber,
                    receivedAt = call.ReceivedAt.ToString("o")
                });
        });

        app.MapMethods("/callerid", ["GET", "POST"], async (HttpRequest request) =>
        {
            string? Get(string name)
            {
                var value = request.Query[name].ToString();
                if (string.IsNullOrEmpty(value) && request.HasFormContentType)
                {
                    value = request.Form[name].ToString();
                }

                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            if (request.HasFormContentType)
            {
                await request.ReadFormAsync();
            }

            HandleEasyHostCall(Get("caller"), Get("devserial"), Get("port"), Get("date"), Get("time"));

            // Easy Host expects exactly this JSON body to treat the forward as successful.
            return Results.Text("{\"RESULT\":\"SUCCESS\"}", "application/json");
        });

        await app.StartAsync();
        _app = app;
        Log.Info($"Local API listening on {BaseUrl}");
    }

    private void HandleEasyHostCall(string? caller, string? devSerial, string? port, string? date, string? time)
    {
        Log.Info($"Easy Host call: caller={caller} devserial={devSerial} port={port} date={date} time={time}");

        var phone = NormalizePhone(caller);
        if (phone.Length < 3)
        {
            Log.Warn($"Ignored call with no usable number (caller='{caller}', likely withheld).");
            return;
        }

        var now = DateTime.Now;
        if (_lastAccepted is { } last &&
            last.PhoneNumber == phone &&
            now - last.ReceivedAt < DuplicateWindow)
        {
            Log.Info($"Ignored duplicate forward for {phone}.");
            return;
        }

        var call = new IncomingCall(phone, now, devSerial, port);
        _lastAccepted = call;
        _store.Add(call);
        Log.Info($"Incoming call stored: {phone}");
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            await _app.StopAsync(TimeSpan.FromSeconds(3));
            await _app.DisposeAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Error stopping local API.", ex);
        }

        _app = null;
        Log.Info("Local API stopped.");
    }
}
