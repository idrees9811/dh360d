; Inno Setup script for DH360D Windows tray app
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppName "DH360D"
#define MyAppPublisher "idrees9811"
#define MyAppExeName "DH360D.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=DH360D-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut to Settings"; GroupDescription: "Additional icons:"
Name: "startup"; Description: "Start DH360D minimized in the tray when Windows starts"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "..\publish\DH360D.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Settings"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "schtasks.exe"; Parameters: "/Create /TN ""DH360D"" /TR ""\""{app}\{#MyAppExeName}\"" --minimized"" /SC ONLOGON /RL HIGHEST /F"; Tasks: startup; Flags: runhidden
; shellexec + runascurrentuser: DH360D.exe requires admin (manifest). CreateProcess fails with error 740 without this.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent shellexec runascurrentuser

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""DH360D"" /F"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\DH360DFeed"