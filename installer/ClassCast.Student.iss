; ============================================================================
;  ClassCast Student - Inno Setup installer script
;
;  Builds a single self-contained setup .exe from the published output in
;  ..\dist\Student (produced by publish.ps1). No .NET runtime is required on
;  the target machine - the .NET 8 runtime is bundled.
;
;  Per-machine install (Program Files, requires admin). The installer:
;    * creates the three inbound Windows Firewall rules,
;    * registers the tray client to auto-start for every user at login
;      (HKLM ...\CurrentVersion\Run),
;  so the app never needs administrator rights at runtime.
;
;  The Student client deliberately resists being closed, so the installer
;  terminates any running instance before installing or uninstalling to avoid
;  locked-file errors.
;
;  Author:  Simon Rundell / CodeMonkey Design Ltd.
;  License: CC BY-NC-SA 4.0
; ============================================================================

#define AppName        "ClassCast Student"
#define AppVersion     "1.0.0"
#define AppPublisher   "CodeMonkey Design Ltd."
#define AppExeName     "ClassCast.Student.exe"
#define SourceDir      "..\dist\Student"

; Firewall ports - keep in step with config.json if you change the defaults.
#define UdpDiscoveryPort  "45678"
#define TcpControlPort    "45679"
#define TcpBroadcastPort  "45680"

[Setup]
; A unique, stable AppId ties upgrades and uninstalls together. Do not reuse
; this GUID for any other product.
AppId={{D2F9C634-8A17-4B6E-A0F3-5C9E1B7D2A48}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\ClassCast\Student
DefaultGroupName=ClassCast
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=Output
OutputBaseFilename=ClassCastStudent-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; config.json is copied only if absent so an upgrade never overwrites a
; school's edited settings.
Source: "{#SourceDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; No desktop shortcut: the client is meant to run silently in the tray.
Name: "{group}\Uninstall ClassCast Student"; Filename: "{uninstallexe}"

[Registry]
; Auto-start the tray client for every user at login.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClassCastStudent"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue

[Run]
; Create the inbound firewall rules (delete first so repeat installs stay
; idempotent).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-UDP-In"" dir=in action=allow protocol=UDP localport={#UdpDiscoveryPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (UDP {#UdpDiscoveryPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Control-In"" dir=in action=allow protocol=TCP localport={#TcpControlPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpControlPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Broadcast-In"" dir=in action=allow protocol=TCP localport={#TcpBroadcastPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpBroadcastPort})..."
; Launch the client immediately so it is running without waiting for the next
; login (silent install via GPO/script skips this).
Filename: "{app}\{#AppExeName}"; Flags: nowait postinstall skipifsilent runasoriginaluser; Description: "Start ClassCast Student now"

[UninstallRun]
; Remove the firewall rules on uninstall.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden; RunOnceId: "DelUdp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden; RunOnceId: "DelControl"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden; RunOnceId: "DelBroadcast"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Code]
{ Terminate any running Student client before files are written, since the
  client resists normal closing and would otherwise lock its own files. }
procedure KillStudent;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExeName}', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    KillStudent;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    KillStudent;
end;
