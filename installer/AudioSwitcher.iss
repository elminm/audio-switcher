; Inno Setup script for Audio Switcher. Build with installer\build.ps1.
#define AppName "Audio Switcher"
#define AppVersion "1.0.0"
#define AppExe "AudioSwitcher.exe"

[Setup]
AppId={{8E2B6F4C-3D1A-4B7E-9C55-2F6A1D3E7B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=elminm
AppPublisherURL=https://github.com/elminm/audio-switcher
AppSupportURL=https://github.com/elminm/audio-switcher/issues
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AudioSwitcher-Setup-{#AppVersion}
SetupIconFile=..\app\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
VersionInfoVersion={#AppVersion}

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
en.StartWithWindows=Start Audio Switcher with Windows
ru.StartWithWindows=Запускать Audio Switcher вместе с Windows
en.DeleteSettings=Also delete your Audio Switcher settings (device priorities) and log?
ru.DeleteSettings=Удалить также настройки Audio Switcher (приоритеты устройств) и журнал?

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\AudioSwitcher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\config.example.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AudioSwitcher"; ValueData: """{app}\{#AppExe}"" --tray"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Flags: nowait; Check: WizardSilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

// Ask a running copy to quit (tray icon removed cleanly), then make sure it is gone.
procedure StopApp(ExePath: String);
var
  rc: Integer;
begin
  if FileExists(ExePath) then
  begin
    Exec(ExePath, '--exit', '', SW_HIDE, ewWaitUntilTerminated, rc);
    Sleep(1500);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, rc);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp(ExpandConstant('{app}\{#AppExe}'));
  Result := '';
end;

// Startup unchecked on an upgrade: drop the old autostart entry.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('startup') then
    RegDeleteValue(HKCU, RunKey, 'AudioSwitcher');
end;

function InitializeUninstall(): Boolean;
begin
  StopApp(ExpandConstant('{app}\{#AppExe}'));
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, RunKey, 'AudioSwitcher');
    if not UninstallSilent and
       (MsgBox(CustomMessage('DeleteSettings'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(ExpandConstant('{userappdata}\AudioSwitcher'), True, True, True);
  end;
end;
