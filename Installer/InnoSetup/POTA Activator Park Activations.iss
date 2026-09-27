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
;      publish\cqZones.json
;      publish\ituZones.json
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
Source: "..\..\publish\cqZones.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\ituZones.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\License.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; \
    Flags: postinstall nowait skipifsilent

[Code]

// Every update is a clean uninstall + reinstall rather than an in-place
// overwrite, so Installed Apps only ever shows one entry and no file dropped
// by a newer version is left behind in the program folder. That covers:
//   - earlier releases of this Inno Setup installer (same AppId),
//   - the old Visual Studio Installer Projects MSI builds, which have their
//     own entries - including the ones from before the rename, when the
//     product was called "POTA Check".
// User data lives in %LocalAppData% (see GetWritableAppDataFolder in
// Form1.cs) and is never touched, so caches and offline maps survive.

const
  UninstallKeyRoot = 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  LegacyAppName = 'POTA Check';

var
  OldInstallDirs: TArrayOfString;

function IsOurProductName(const Name: String): Boolean;
begin
  Result := (CompareText(Name, '{#MyAppName}') = 0) or
            (CompareText(Name, LegacyAppName) = 0);
end;

procedure RememberOldInstallDir(const Dir: String);
var
  N: Integer;
begin
  if Dir = '' then Exit;
  N := GetArrayLength(OldInstallDirs);
  SetArrayLength(OldInstallDirs, N + 1);
  OldInstallDirs[N] := Dir;
end;

// Only ever wipes a folder that is clearly this app's own (named after the
// product), never some general-purpose folder the user may have picked on
// the directory page.
procedure DeleteAppFolder(Dir: String);
begin
  Dir := RemoveBackslashUnlessRoot(Dir);
  if (Dir <> '') and DirExists(Dir) and IsOurProductName(ExtractFileName(Dir)) then
  begin
    DelTree(Dir, True, True, True);
    // The K5JSG publisher folder above it - RemoveDir only succeeds if empty
    RemoveDir(ExtractFileDir(Dir));
  end;
end;

// Belt and braces: an Inno uninstaller re-launches itself from %TEMP%, and
// deletes its own unins*.exe as the very last step, so this confirms it has
// really finished before the new files go in.
procedure WaitForFileGone(const FileName: String; TimeoutMs: Integer);
begin
  while FileExists(FileName) and (TimeoutMs > 0) do
  begin
    Sleep(250);
    TimeoutMs := TimeoutMs - 250;
  end;
end;

// Silently uninstalls every installed copy of this app registered under
// RootKey. Returns an error message, or '' if everything went fine.
function UninstallOldVersions(RootKey: Integer): String;
var
  Names: TArrayOfString;
  I, ResultCode: Integer;
  Key, DisplayName, Publisher, Location, Uninstaller: String;
  IsMsi: Cardinal;
begin
  Result := '';
  if not RegGetSubkeyNames(RootKey, UninstallKeyRoot, Names) then Exit;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    Key := UninstallKeyRoot + '\' + Names[I];
    if RegQueryStringValue(RootKey, Key, 'DisplayName', DisplayName) and
       IsOurProductName(DisplayName) and
       RegQueryStringValue(RootKey, Key, 'Publisher', Publisher) and
       (CompareText(Publisher, '{#MyAppPublisher}') = 0) then
    begin
      if RegQueryStringValue(RootKey, Key, 'InstallLocation', Location) then
        RememberOldInstallDir(Location);

      if RegQueryDWordValue(RootKey, Key, 'WindowsInstaller', IsMsi) and (IsMsi = 1) then
      begin
        // Old MSI build: the subkey name is its ProductCode. VS Installer
        // Projects didn't record InstallLocation, so fall back to its default.
        RememberOldInstallDir(ExpandConstant('{autopf}\{#MyAppPublisher}\') + DisplayName);
        if not Exec(ExpandConstant('{sys}\msiexec.exe'),
                    '/x ' + Names[I] + ' /qn /norestart',
                    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
          ResultCode := -1;
        // 1605 = already gone, 3010 = done but wants a reboot
        if (ResultCode <> 0) and (ResultCode <> 1605) and (ResultCode <> 3010) then
        begin
          Result := Format('%s could not be removed automatically (error %d).', [DisplayName, ResultCode]);
          Exit;
        end;
      end
      else if RegQueryStringValue(RootKey, Key, 'UninstallString', Uninstaller) then
      begin
        Uninstaller := RemoveQuotes(Uninstaller);
        RememberOldInstallDir(ExtractFileDir(Uninstaller));
        if FileExists(Uninstaller) then
        begin
          if not Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART',
                      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
            ResultCode := -1;
          if ResultCode <> 0 then
          begin
            Result := Format('%s could not be removed automatically (error %d).', [DisplayName, ResultCode]);
            Exit;
          end;
          WaitForFileGone(Uninstaller, 60000);
        end;
        // An entry whose uninstaller is missing (folder deleted by hand)
        // would otherwise linger in Installed Apps forever.
        if RegKeyExists(RootKey, Key) then
          RegDeleteKeyIncludingSubkeys(RootKey, Key);
      end;
    end;
  end;
end;

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
var
  I: Integer;
  ResultCode: Integer;
begin
  StopRunningApp();
  Exec(ExpandConstant('{cmd}'),
       '/C taskkill /F /IM "' + LegacyAppName + '.exe" >nul 2>&1',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Result := UninstallOldVersions(HKLM64);
  if Result = '' then Result := UninstallOldVersions(HKLM32);
  if Result = '' then Result := UninstallOldVersions(HKCU);

  if Result <> '' then
  begin
    Result := Result + #13#10#13#10 +
      'Please uninstall it from Settings > Apps > Installed apps, then run this setup again.';
    Exit;
  end;

  // Anything the old uninstallers left behind (files they didn't install
  // themselves, or files from versions older than their own records)
  for I := 0 to GetArrayLength(OldInstallDirs) - 1 do
    DeleteAppFolder(OldInstallDirs[I]);
  DeleteAppFolder(ExpandConstant('{app}'));
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    DeleteAppFolder(ExpandConstant('{app}'));
end;
