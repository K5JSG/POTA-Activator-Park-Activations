; ===================================================================
;  POTA Activator Park Activations - installer
;
;  Build with Inno Setup 6 or newer (run from the repo root):
;      iscc "Installer\InnoSetup\POTA Activator Park Activations.iss"
;
;  Expects the published self-contained single-file exe and its loose
;  data files at:
;      publish\POTA Activator Park Activations.exe
;      publish\counties.json
;      publish\HelpContent.rtf
;      publish\ParkElevations.csv
;  (build.ps1, at the repo root, puts them there)
; ===================================================================

#define MyAppName "POTA Activator Park Activations"
#define MyAppPublisher "K5JSG"
#define MyAppURL "https://github.com/K5JSG/POTA-Activator-Park-Activations"
#define MyAppExeName "POTA Activator Park Activations.exe"

; Overridable from the command line: iscc /DMyAppVersion=1.4.0 ...
#ifndef MyAppVersion
  #define MyAppVersion "1.4.0"
#endif

[Setup]
; Keep this GUID stable forever: it is how Windows recognises an upgrade of
; the same product rather than a second installation. Deliberately a fresh
; GUID, unrelated to the old Visual Studio Installer Projects (.vdproj)
; build's ProductCode/UpgradeCode - that was a different installer
; technology entirely, and its ProductCode changed on every release anyway
; (VS Installer Projects regenerates it per version, unlike this one).
AppId={{62A39724-A285-4A61-A519-B6C98BA075B2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}

DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
LicenseFile=..\..\License.txt

; Writing to Program Files needs admin - the app itself does not require
; elevation to run (see GetWritableAppDataFolder in Form1.cs, which is
; exactly why %LocalAppData% is used for all user data), just to install.
PrivilegesRequired=admin

OutputDir=..\..\dist
OutputBaseFilename={#MyAppName} Setup {#MyAppVersion}
SetupIconFile=..\..\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809 or newer
MinVersion=10.0.17763

; Offer to shut the app down instead of demanding a reboot
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; \
    GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\counties.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\HelpContent.rtf"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\ParkElevations.csv"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\License.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; \
    Flags: postinstall nowait skipifsilent

[Code]

// Stop a running instance before installing or uninstalling, otherwise the
// exe is locked and the file copy fails.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'),
       '/C taskkill /F /IM "{#MyAppExeName}" >nul 2>&1',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;
