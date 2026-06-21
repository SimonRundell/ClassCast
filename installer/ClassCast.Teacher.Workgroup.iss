; ============================================================================
;  ClassCast Teacher (Workgroup) - Inno Setup installer script
;
;  Same Teacher application as the standard installer, but configured for
;  machines that are NOT joined to an Active Directory domain. Instead of an AD
;  sign-in it uses a single shared teacher password.
;
;  The application files come from the shared published output in ..\dist\Teacher
;  (produced by publish.ps1); only the bundled config.json differs - this
;  installer ships ..\installer\config.workgroup.json (authMode = Workgroup).
;
;  During setup the installer asks for a teacher password and, because it runs
;  elevated, calls the Teacher exe with --set-teacher-password to write the
;  salted hash into config.json. The app itself (which runs without admin
;  rights) then only ever reads that hash.
;
;  Per-machine install (Program Files, requires admin). Creates the three
;  inbound Windows Firewall rules.
;
;  Author:  Simon Rundell / CodeMonkey Design Ltd.
;  License: CC BY-NC-SA 4.0
; ============================================================================

#define AppName        "ClassCast Teacher (Workgroup)"
#define AppVersion     "1.0.0"
#define AppPublisher   "CodeMonkey Design Ltd."
#define AppExeName     "ClassCast.Teacher.exe"
#define SourceDir      "..\dist\Teacher"
#define MinPasswordLen 6

; Firewall ports - keep in step with config.workgroup.json if you change them.
#define UdpDiscoveryPort  "45678"
#define TcpControlPort    "45679"
#define TcpBroadcastPort  "45680"

[Setup]
; A unique, stable AppId - distinct from the domain Teacher installer so both
; could in principle coexist. Do not reuse this GUID for any other product.
AppId={{F3A9D7B1-6C24-4E59-8B0A-2D7E5F1C9A63}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\ClassCast\TeacherWorkgroup
DefaultGroupName=ClassCast
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=Output
OutputBaseFilename=ClassCastTeacherWorkgroup-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Workgroup config.json (only if absent, so upgrades keep the saved password).
Source: "config.workgroup.json"; DestDir: "{app}"; DestName: "config.json"; Flags: onlyifdoesntexist uninsneveruninstall
; The Teacher application, .NET runtime and ffmpeg - shared with the AD build.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ClassCast Teacher (Workgroup)"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall ClassCast Teacher (Workgroup)"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ClassCast Teacher"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Firewall rules (delete first so repeat installs stay idempotent).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-UDP-In"" dir=in action=allow protocol=UDP localport={#UdpDiscoveryPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (UDP {#UdpDiscoveryPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Control-In"" dir=in action=allow protocol=TCP localport={#TcpControlPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpControlPort})..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ClassCast-Broadcast-In"" dir=in action=allow protocol=TCP localport={#TcpBroadcastPort} profile=domain,private enable=yes"; Flags: runhidden; StatusMsg: "Adding firewall rule (TCP {#TcpBroadcastPort})..."
; Store the shared teacher password (elevated, so it can write into Program Files).
Filename: "{app}\{#AppExeName}"; Parameters: "--set-teacher-password ""{code:GetTeacherPassword}"""; Flags: runhidden waituntilterminated; StatusMsg: "Setting the teacher password..."; Check: HavePassword
; Offer to launch at the end of setup.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,ClassCast Teacher}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-UDP-In"""; Flags: runhidden; RunOnceId: "DelUdp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Control-In"""; Flags: runhidden; RunOnceId: "DelControl"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ClassCast-Broadcast-In"""; Flags: runhidden; RunOnceId: "DelBroadcast"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Code]
var
  PasswordPage: TInputQueryWizardPage;

{ Build a wizard page that collects the shared teacher password (entry +
  confirmation, both masked). }
procedure InitializeWizard;
begin
  PasswordPage := CreateInputQueryPage(wpSelectDir,
    'Teacher password',
    'Set the shared password for the ClassCast Teacher console',
    'This password protects the Teacher console on this workgroup machine. ' +
    'Teachers will enter it to start ClassCast. You can change it later by ' +
    'reinstalling.');
  PasswordPage.Add('Password:', True);
  PasswordPage.Add('Confirm password:', True);
end;

{ Enforce a minimum length and matching confirmation before leaving the page. }
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = PasswordPage.ID then
  begin
    if Length(PasswordPage.Values[0]) < {#MinPasswordLen} then
    begin
      MsgBox('The password must be at least {#MinPasswordLen} characters long.',
        mbError, MB_OK);
      Result := False;
    end
    else if PasswordPage.Values[0] <> PasswordPage.Values[1] then
    begin
      MsgBox('The passwords do not match. Please re-enter them.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

{ Supplies the entered password to the [Run] entry that stores its hash. }
function GetTeacherPassword(Param: String): String;
begin
  Result := PasswordPage.Values[0];
end;

{ Only run the password-setting step when a password was actually entered
  (it always is in an interactive install; this guards silent installs). }
function HavePassword: Boolean;
begin
  Result := Length(PasswordPage.Values[0]) > 0;
end;
