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
Name: "installextension"; Description: "Install VS Code extension"; GroupDescription: "VS Code Integration:"; Flags: checked; Check: VSCodeExists

[Files]
; API/Service
Source: "..\..\publish\win-x64\api\*"; DestDir: "{app}\api"; Flags: ignoreversion recursesubdirs createallsubdirs
; Tray application
Source: "..\..\publish\win-x64\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
; Agents
Source: "..\..\publish\win-x64\agents\*"; DestDir: "{app}\agents"; Flags: ignoreversion recursesubdirs createallsubdirs
; VS Code Extension
Source: "..\..\publish\win-x64\extension\*.vsix"; DestDir: "{app}\extension"; Flags: ignoreversion
; Scripts
Source: "..\..\publish\win-x64\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion
; PostgreSQL
Source: "..\..\publish\win-x64\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs createallsubdirs
; Version info
Source: "..\..\publish\win-x64\version.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Tray"; Filename: "{app}\tray\{#MyTrayExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
; Add tray app to auto-start
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AuraTray"; ValueData: """{app}\tray\{#MyTrayExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostartray

[Run]
; Initialize PostgreSQL database (first install only)
Filename: "{app}\pgsql\bin\initdb.exe"; Parameters: "-D ""{app}\data"" -U postgres -E UTF8 --locale=en_US.UTF-8"; Flags: runhidden; Check: not DatabaseExists
; Register PostgreSQL as Windows service
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "register -N AuraDB -D ""{app}\data"" -o ""-p 5433"""; Flags: runhidden; Check: not AuraDBServiceExists
; Start PostgreSQL service
Filename: "sc.exe"; Parameters: "start AuraDB"; Flags: runhidden
; Create auradb database (wait for service to start)
Filename: "{app}\pgsql\bin\createdb.exe"; Parameters: "-h localhost -p 5433 -U postgres auradb"; Flags: runhidden; Check: not DatabaseExists
; Enable pgvector extension
Filename: "{app}\pgsql\bin\psql.exe"; Parameters: "-h localhost -p 5433 -U postgres -d auradb -c ""CREATE EXTENSION IF NOT EXISTS vector"""; Flags: runhidden
; Install as Windows Service
Filename: "sc.exe"; Parameters: "create AuraService binPath= ""{app}\api\{#MyAppExeName}"" start= auto"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "description AuraService ""Aura local AI assistant service"""; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "start AuraService"; Flags: runhidden; Tasks: installservice
; Install VS Code extension
Filename: "{code:GetVSCodePath}"; Parameters: "--install-extension ""{app}\extension\aura-{#MyAppVersion}.vsix"" --force"; Flags: runhidden nowait; Tasks: installextension
; Start tray app
Filename: "{app}\tray\{#MyTrayExeName}"; Parameters: "--minimized"; Flags: nowait postinstall; Tasks: starttray

[UninstallRun]
; Stop and remove Windows Service
Filename: "sc.exe"; Parameters: "stop AuraService"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete AuraService"; Flags: runhidden
; Stop and remove PostgreSQL service
Filename: "sc.exe"; Parameters: "stop AuraDB"; Flags: runhidden
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "unregister -N AuraDB"; Flags: runhidden

[Code]
function DatabaseExists(): Boolean;
begin
  // Check if the data directory already exists (upgrade scenario)
  Result := DirExists(ExpandConstant('{app}\data'));
end;

function AuraDBServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  // Check if AuraDB service already exists
  Result := Exec('sc.exe', 'query AuraDB', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function VSCodeExists(): Boolean;
begin
  // Check common VS Code locations
  Result := FileExists(ExpandConstant('{localappdata}\Programs\Microsoft VS Code\bin\code.cmd')) or
            FileExists('C:\Program Files\Microsoft VS Code\bin\code.cmd') or
            FileExists(ExpandConstant('{localappdata}\Programs\Microsoft VS Code Insiders\bin\code-insiders.cmd'));
end;

function GetVSCodePath(Param: string): string;
begin
  if FileExists(ExpandConstant('{localappdata}\Programs\Microsoft VS Code\bin\code.cmd')) then
    Result := ExpandConstant('{localappdata}\Programs\Microsoft VS Code\bin\code.cmd')
  else if FileExists('C:\Program Files\Microsoft VS Code\bin\code.cmd') then
    Result := 'C:\Program Files\Microsoft VS Code\bin\code.cmd'
  else if FileExists(ExpandConstant('{localappdata}\Programs\Microsoft VS Code Insiders\bin\code-insiders.cmd')) then
    Result := ExpandConstant('{localappdata}\Programs\Microsoft VS Code Insiders\bin\code-insiders.cmd')
  else
    Result := 'code';  // Fallback to PATH
end;

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

  // Inform about VS Code if not detected
  if not VSCodeExists() then
  begin
    MsgBox('VS Code was not detected. The Aura extension will be placed in the installation folder.' + #13#10 + #13#10 +
           'You can manually install it later by running:' + #13#10 +
           'powershell -File "' + ExpandConstant('{app}') + '\scripts\install-extension.ps1"',
           mbInformation, MB_OK);
  end;
end;
