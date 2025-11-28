; Aura Windows Installer
; Requires Inno Setup 6.0 or later
; Download: https://jrsoftware.org/isdl.php

#define MyAppName "Aura"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "John Azariah"
#define MyAppURL "https://github.com/johnazariah/aura"
#define MyAppExeName "Aura.Api.exe"
#define MyTrayExeName "Aura.Tray.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\publish\installers
OutputBaseFilename=Aura-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "installservice"; Description: "Install as Windows Service (auto-start on boot)"; GroupDescription: "Service Options:"
Name: "starttray"; Description: "Start system tray monitor after installation"; GroupDescription: "Tray Options:"; Flags: checked
Name: "autostartray"; Description: "Start system tray monitor with Windows"; GroupDescription: "Tray Options:"; Flags: checked

[Files]
; API/Service
Source: "..\..\publish\win-x64\api\*"; DestDir: "{app}\api"; Flags: ignoreversion recursesubdirs createallsubdirs
; Tray application
Source: "..\..\publish\win-x64\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
; Agents
Source: "..\..\publish\win-x64\agents\*"; DestDir: "{app}\agents"; Flags: ignoreversion recursesubdirs createallsubdirs
; Version info
Source: "..\..\publish\win-x64\version.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Tray"; Filename: "{app}\tray\{#MyTrayExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
; Add tray app to auto-start
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AuraTray"; ValueData: """{app}\tray\{#MyTrayExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostartray

[Run]
; Install as Windows Service
Filename: "sc.exe"; Parameters: "create AuraService binPath= ""{app}\api\{#MyAppExeName}"" start= auto"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "description AuraService ""Aura local AI assistant service"""; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "start AuraService"; Flags: runhidden; Tasks: installservice
; Start tray app
Filename: "{app}\tray\{#MyTrayExeName}"; Parameters: "--minimized"; Flags: nowait postinstall; Tasks: starttray

[UninstallRun]
; Stop and remove Windows Service
Filename: "sc.exe"; Parameters: "stop AuraService"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete AuraService"; Flags: runhidden

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Check for Ollama
  if not FileExists(ExpandConstant('{localappdata}\Programs\Ollama\ollama.exe')) and
     not FileExists('C:\Program Files\Ollama\ollama.exe') then
  begin
    if MsgBox('Ollama is required but not detected. ' +
              'Please install Ollama from https://ollama.com before continuing.' + #13#10 + #13#10 +
              'Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;
