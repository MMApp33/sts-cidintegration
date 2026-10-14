# ScanToServe CID Easy Connector

Windows tray app that bridges **CID Easy / Easy Host** caller ID into **ScanToServe**.

Easy Host forwards each incoming call to a local HTTP endpoint. This connector receives those calls and exposes them to the ScanToServe admin site (Phone Integration) using the same local API contract as the router connector.

## Requirements

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download) — only needed to *build*
- [CID Easy Easy Host](https://cideasy.com/) installed and run at least once (on the client PC)
- ScanToServe account with Phone Integration enabled

## Build and run (dev)

```powershell
cd STS.CID.Easy
dotnet build
dotnet run
```

The app starts in the system tray. Double-click the tray icon (or right-click → **Open**) for the window. Closing the window keeps it in the tray; **Exit** quits.

## Ship to clients (normal Windows installer)

Clients get a normal **Setup.exe** (wizard → Program Files → Start Menu → Apps & Features uninstall). They do **not** need PowerShell or .NET.

### One-time on your PC (to build the installer)

1. Install free [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. Double-click `publish.bat`  
   (or open `installer\ScanToServe.CidEasy.iss` in Inno Setup → **Compile**)

Output:

`artifacts\ScanToServe-CID-Easy-Setup.exe`

### Give it to clients

1. Create a **GitHub Release**
2. Upload `ScanToServe-CID-Easy-Setup.exe`
3. Send the release download URL

Client: download → double-click Setup → Next/Next/Finish. Same as other Windows software.

After install it starts minimized to the tray, and it also starts automatically when Windows signs in (same as the **Start with Windows** checkbox in the app — clients can turn that off).

No desktop icon is created (Start Menu only). The Setup and app use `Assets\app.ico`.

Ship only: `artifacts\ScanToServe-CID-Easy-Setup.exe` (the other files in `artifacts` are build leftovers).

## Setup

1. **Start this connector**  
   Confirm **Local API** shows listening (default `http://127.0.0.1:5050`).

2. **Configure Easy Host**  
   Click **Configure Easy Host**. That writes the forward URL into Easy Host’s registry settings and enables web POST if needed.  
   Restart Easy Host (or the PC) so the change takes effect.

3. **Point ScanToServe at the connector**  
   In ScanToServe: **Settings → Integrations → Phone Integration**  
   Set the URL to the value shown in the app (e.g. `http://127.0.0.1:5050`).

4. **Verify**  
   Click **Send test call**. A test number should appear under **Recent calls**.  
   You can also use **Copy URL** / **Copy forward URL** for manual setup.

## How it works

```
Phone line → Easy Host → http://127.0.0.1:<port>/callerid
                              ↓
                     STS.CID.Easy (this app)
                              ↓
              ScanToServe polls /api/latest-call
```

| Endpoint | Purpose |
| --- | --- |
| `GET/POST /callerid` | Easy Host posts caller ID here |
| `GET /api/status` | Connection status for ScanToServe |
| `GET /api/latest-call` | Latest call for ScanToServe (consumed once) |

## Settings

Persisted next to the executable as `settings.json`:

| Key | Default | Description |
| --- | --- | --- |
| `apiPort` | `5050` | Local API port |
| `startWithWindows` | `true` | Launch at Windows sign-in |
| `startMinimized` | `true` | Start in tray only |
| `allowedOrigins` | ScanToServe + localhost | CORS origins allowed to call the local API |

Example:

```json
{
  "apiPort": 5050,
  "startWithWindows": true,
  "startMinimized": true,
  "allowedOrigins": [
    "https://scantoserve.com",
    "https://*.scantoserve.com",
    "http://localhost:3000"
  ]
}
```

Pass `--minimized` on the command line to force tray start.

## Logs

Daily log files live in a `Logs` folder beside the executable:

`Logs/CidEasy-YYYY-MM-DD.log`

Use **View logs** in the UI or tray menu to open that folder.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Local API not running | Another process may be using the port — change `apiPort` in `settings.json` |
| Easy Host settings not found | Install Easy Host and run it once so it creates its registry keys |
| Forwarding not configured | Click **Configure Easy Host**, then restart Easy Host |
| Easy Host not running | Start Easy Host so calls are detected and forwarded |
| Test call fails | Confirm Local API is listening; check the log file |
| ScanToServe does not see calls | Confirm Phone Integration URL matches this app’s base URL |

## Project layout

```
STS.CID.Easy/
  Program.cs          Entry point, single-instance mutex
  MainForm.cs         Tray UI
  LocalApiServer.cs   Kestrel endpoints
  EasyHost.cs         Registry configure / detect
  CallStore.cs        Pending call + history
  AppSettings.cs      settings.json
  Log.cs              Rolling file log
```

## License / product

Part of the ScanToServe stack — https://scantoserve.com
