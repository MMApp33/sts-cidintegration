; Compiles to a normal Windows Setup.exe (wizard → Program Files → Start Menu → Uninstall).
; Build with publish.bat, or open this file in Inno Setup and click Compile.

#define MyAppName "ScanToServe CID Easy"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ScanToServe"
#define MyAppURL "https://scantoserve.com"
#define MyAppExeName "STS.CID.Easy.exe"

[Setup]
AppId={{8F3A2C1D-9E4B-4A7F-B2D1-6C5E8F0A1234}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\ScanToServe\CID Easy
DefaultGroupName=ScanToServe
DisableProgramGroupPage=yes
SetupIconFile=..\STS.CID.Easy\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
OutputDir=..\artifacts
OutputBaseFilename=ScanToServe-CID-Easy-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Published app folder produced by publish.bat
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu only — no desktop icon
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Start automatically at Windows sign-in for all users (minimized to tray).
; HKLM is used because Setup installs to Program Files as admin.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScanToServe CID Easy"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--minimized"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: files; Name: "{app}\settings.json"

