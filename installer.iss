; Undertone — Inno Setup Installer Script
; Builds a setup.exe from the publish\win-x64\ output.
; Compile with scripts\build-windows-installer.ps1 (also accepts an existing publish folder).

#define MyAppName "Undertone"
#ifndef MyAppVersion
  #define MyAppVersion "1.8.2"
#endif
#ifndef PublishDir
  #define PublishDir "publish\win-x64"
#endif
#define MyAppPublisher "Mayank Praveen Kumar"
#define MyAppExeName "Undertone.exe"
#define MyAppURL "https://github.com/Mayank-Praveen-Kumar/Undertone"

[Setup]
AppId={{E8A3B5F1-7C2D-4A9E-B6F0-1D3E5A7C9B2F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases/latest
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer-output
OutputBaseFilename=Undertone-v{#MyAppVersion}-Setup
SetupIconFile=src\Undertone\Assets\Icons\Undertone.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Moderate compression keeps installer creation and extraction reasonable on laptops.
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.19041
PrivilegesRequired=lowest
; Inno 6.5+ turns on Windows RedirectionGuard for Setup, and on current Windows builds the app
; Setup launches (finish page or the in-app updater's relaunch) inherits it. That session's
; yt-dlp then dies with WinError 448 on any user-made junction in PATH (e.g. scoop's
; apps\*\current), so every YouTube search/download fails until Undertone is restarted. The guard
; protects elevated installers; this one never elevates (lowest), so it buys nothing here.
RedirectionGuard=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Tell Explorer associations changed so the Open-with list refreshes without a re-login.
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Add Undertone to &Open with / Default apps for audio files"; GroupDescription: "File types:"

[Registry]
; Per-user (PrivilegesRequired=lowest, so HKA = HKCU). Mirrors Helpers/WindowsFileAssociations.cs,
; which does the same for portable / winget installs from Settings -> General. Windows 10/11
; never let an installer take the default itself; these keys make Undertone a candidate.
Root: HKA; Subkey: "Software\Classes\Undertone.AudioFile"; ValueType: string; ValueName: ""; ValueData: "Audio File"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Undertone.AudioFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Undertone.AudioFile\shell\open"; ValueType: string; ValueName: ""; ValueData: "Play with Undertone"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Undertone.AudioFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Undertone"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Music player"; Tasks: fileassoc
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Undertone"; ValueData: "Software\Undertone\Capabilities"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".oga"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.oga\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aiff"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.aiff\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aif"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.aif\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aifc"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.aifc\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ape"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.ape\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wv"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.wv\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".alac"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.alac\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dsf"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.dsf\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dff"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.dff\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Undertone\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4b"; ValueData: "Undertone.AudioFile"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.m4b\OpenWithProgids"; ValueType: none; ValueName: "Undertone.AudioFile"; Flags: uninsdeletevalue; Tasks: fileassoc

[Files]
; Copy everything CI published rather than an allowlist of patterns. The old
; hand-written list (Undertone.exe + *.dll + libvlc\*) silently omitted ffmpeg.exe, which
; CI pins and SHA-256 verifies specifically so users don't need a system install — so
; every winget / Chocolatey / Setup.exe user lost the Audio Converter, the ReplayGain
; scanner and share-clip video export unless they happened to have ffmpeg on PATH.
; A wildcard cannot drift from what CI actually produces.
; *.pdb is excluded: it ships full source paths and internal symbol names to end users,
; and nothing in the app consumes it.
; This is an x64 player; NuGet also publishes unused x86/ARM VLC copies.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.log,plugins.dat,plugins.dat.undertone,\libvlc\win-arm64\*,\libvlc\win-x86\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
; libvlc's plugins cache, written by the app on first start (Services/VlcPluginCache.cs).
Type: files; Name: "{app}\libvlc\win-x64\plugins\plugins.dat"
Type: files; Name: "{app}\libvlc\win-x64\plugins\plugins.dat.undertone"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Check: LaunchAfterSetup

[Code]
// Used by unattended deployment and install/uninstall verification. Normal installs
// keep the finish-page launch checkbox; the in-app updater still relaunches below.
function LaunchAfterSetup(): Boolean;
var
  I: Integer;
begin
  Result := True;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/NOLAUNCH') = 0 then
      Result := False;
end;

// On silent installs (the in-app updater path), Inno skips every [Run] entry
// flagged `postinstall skipifsilent`, so the app would never relaunch after
// "Install & Restart". Launch the new exe ourselves at ssDone in that case.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssDone) and WizardSilent() and LaunchAfterSetup() then
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '', '',
         SW_SHOW, ewNoWait, ResultCode);
end;
