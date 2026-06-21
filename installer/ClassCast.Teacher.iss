; ============================================================================
;  ClassCast Teacher - Inno Setup installer script
;
;  Builds a single self-contained setup .exe from the published output in
;  ..\dist\Teacher (produced by publish.ps1). No .NET runtime is required on
;  the target machine - the .NET 8 runtime and ffmpeg are bundled.
;
;  Per-machine install (Program Files, requires admin). The installer creates
;  the three inbound Windows Firewall rules itself, so the app never needs
;  administrator rights at runtime.
;
;  Author:  Simon Rundell / CodeMonkey Design Ltd.
;  License: CC BY-NC-SA 4.0
; ============================================================================

#define AppName        "ClassCast Teacher"
#define AppVersion     "1.0.0"
#define AppPublisher   "CodeMonkey Design Ltd."
#define AppExeName     "ClassCast.Teacher.exe"
#define SourceDir      "..\dist\Teacher"

; Firewall ports - keep in step with config.json if you change the defaults.
#define UdpDiscoveryPort  "45678"
#define TcpControlPort    "45679"
#define TcpBroadcastPort  "45680"

[Setup]
; A unique, stable AppId ties upgrades and uninstalls together. Do not reuse
; this GUID for any other product.
AppId={{B7E4A1C2-3F5D-4A8B-9C1E-7A2D6F0B4E91}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\ClassCast\Teacher
DefaultGroupName=ClassCast
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=Output
OutputBaseFilename=ClassCastTeacher-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Self-contained build is x64-only; refuse to install on anything else.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Everything published into dist\Teacher (app + .NET runtime + ffmpeg).
; config.json is copied only if absent so an upgrade never overwrites a
; school's edited settings.
Source: "{#SourceDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ClassCast Teacher"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall ClassCast Teacher"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ClassCast Teacher"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Create the inbound firewall rules (delete first so repeat installs stay
; idempotent). RunOnceId guarantees each rule is added only once per install.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-UDP-In"" dir=in action=allow protocol=UDP localport={#UdpDiscoveryPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (UDP {#UdpDiscoveryPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Control-In"" dir=in action=allow protocol=TCP localport={#TcpControlPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpControlPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Broadcast-In"" dir=in action=allow protocol=TCP localport={#TcpBroadcastPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpBroadcastPort})..."
; Offer to launch the Teacher app at the end of setup.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,ClassCast Teacher}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the firewall rules on uninstall.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden; RunOnceId: "DelUdp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden; RunOnceId: "DelControl"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden; RunOnceId: "DelBroadcast"

[UninstallDelete]
; Remove logs written beside the executable so no stray files are left behind.
Type: filesandordirs; Name: "{app}\logs"
