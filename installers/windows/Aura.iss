; Aura Windows Installer
; Requires Inno Setup 6.0 or later
; Download: https://jrsoftware.org/isdl.php

#define MyAppName "Aura"
; Version can be overridden via command line: ISCC /DMyAppVersion=X.Y.Z
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
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
; Always create install log in %TEMP%\Aura-Setup-{version}.log
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "installservice"; Description: "Install as Windows Service (auto-start on boot)"; GroupDescription: "Service Options:"
Name: "starttray"; Description: "Start system tray monitor after installation"; GroupDescription: "Tray Options:"
Name: "autostartray"; Description: "Start system tray monitor with Windows"; GroupDescription: "Tray Options:"
Name: "installextension"; Description: "Install VS Code extension"; GroupDescription: "VS Code Integration:"; Check: VSCodeExists

[Files]
; API/Service
Source: "..\..\publish\win-x64\api\*"; DestDir: "{app}\api"; Flags: ignoreversion recursesubdirs createallsubdirs
; Tray application
Source: "..\..\publish\win-x64\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
; Agents
Source: "..\..\publish\win-x64\agents\*"; DestDir: "{app}\agents"; Flags: ignoreversion recursesubdirs createallsubdirs
; Prompts
Source: "..\..\publish\win-x64\prompts\*"; DestDir: "{app}\prompts"; Flags: ignoreversion recursesubdirs createallsubdirs
; Patterns
Source: "..\..\publish\win-x64\patterns\*"; DestDir: "{app}\patterns"; Flags: ignoreversion recursesubdirs createallsubdirs
; VS Code Extension
Source: "..\..\publish\win-x64\extension\*.vsix"; DestDir: "{app}\extension"; Flags: ignoreversion
; Scripts
Source: "..\..\publish\win-x64\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion
; Diagnostic script
Source: "Diagnose-Aura.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
; PostgreSQL
Source: "..\..\publish\win-x64\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs createallsubdirs
; Version info
Source: "..\..\publish\win-x64\version.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Tray"; Filename: "{app}\tray\{#MyTrayExeName}"
Name: "{group}\Diagnose {#MyAppName}"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\Diagnose-Aura.ps1"""; Comment: "Check Aura installation status"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
; Add tray app to auto-start
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AuraTray"; ValueData: """{app}\tray\{#MyTrayExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostartray

[Run]
; Initialize PostgreSQL database (first install only)
Filename: "{app}\pgsql\bin\initdb.exe"; Parameters: "-D ""{commonappdata}\Aura\data"" -U postgres -E UTF8"; Flags: runhidden; Check: not DataDirectoryExists
; Register PostgreSQL as Windows service
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "register -N AuraDB -D ""{commonappdata}\Aura\data"" -o ""-p 5433"""; Flags: runhidden; Check: not AuraDBServiceExists
; Start PostgreSQL service
Filename: "sc.exe"; Parameters: "start AuraDB"; Flags: runhidden
; Wait for PostgreSQL to be ready (up to 30 seconds)
Filename: "{app}\pgsql\bin\pg_isready.exe"; Parameters: "-h localhost -p 5433 -t 30"; Flags: runhidden
; Create auradb database (only if it doesn't exist - safe to run on upgrade)
Filename: "{app}\pgsql\bin\createdb.exe"; Parameters: "-h localhost -p 5433 -U postgres auradb"; Flags: runhidden; Check: not AuraDbDatabaseExists
; Enable pgvector extension
Filename: "{app}\pgsql\bin\psql.exe"; Parameters: "-h localhost -p 5433 -U postgres -d auradb -c ""CREATE EXTENSION IF NOT EXISTS vector"""; Flags: runhidden

; Create dedicated service account (provides proper user context for all languages)
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\Create-ServiceAccount.ps1"""; Flags: runhidden; Tasks: installservice
; Install as Windows Service with dedicated account
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\Install-AuraService.ps1"" -InstallPath ""{app}"""; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "description AuraService ""Aura local AI assistant service"""; Flags: runhidden; Tasks: installservice
; Set environment for service to use Production settings
Filename: "reg.exe"; Parameters: "add ""HKLM\SYSTEM\CurrentControlSet\Services\AuraService"" /v Environment /t REG_MULTI_SZ /d ""ASPNETCORE_ENVIRONMENT=Production"" /f"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "start AuraService"; Flags: runhidden; Tasks: installservice
; Install VS Code extension using helper script (finds VSIX dynamically)
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\install-extension.ps1"""; Flags: runhidden nowait; Tasks: installextension
; Start tray app
Filename: "{app}\tray\{#MyTrayExeName}"; Parameters: "--minimized"; Flags: nowait postinstall; Tasks: starttray
; Offer to run diagnostics if user wants to verify installation
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\Diagnose-Aura.ps1"""; Description: "Run installation diagnostics"; Flags: postinstall nowait skipifsilent unchecked

[UninstallRun]
; Stop and remove Windows Service
Filename: "sc.exe"; Parameters: "stop AuraService"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete AuraService"; Flags: runhidden
; Stop and remove PostgreSQL service
Filename: "sc.exe"; Parameters: "stop AuraDB"; Flags: runhidden
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "unregister -N AuraDB"; Flags: runhidden

[Code]
var
  Port5433InUse: Boolean;
  Port5300InUse: Boolean;

function DataDirectoryExists(): Boolean;
begin
  // Check if the data directory already exists in ProgramData (upgrade scenario)
  Result := DirExists(ExpandConstant('{commonappdata}\Aura\data'));
end;

function AuraDBServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  // Check if AuraDB service already exists
  Result := Exec('sc.exe', 'query AuraDB', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function AuraDbDatabaseExists(): Boolean;
var
  ResultCode: Integer;
begin
  // Check if auradb database already exists by querying it
  // pg_isready with database check - returns 0 if can connect to specific database
  Result := FileExists(ExpandConstant('{app}\pgsql\bin\psql.exe')) and 
            Exec(ExpandConstant('{app}\pgsql\bin\psql.exe'), 
                 '-h localhost -p 5433 -U postgres -d auradb -c "SELECT 1" -t', 
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsPortInUse(Port: Integer): Boolean;
var
  ResultCode: Integer;
  TempFile: String;
  Lines: TArrayOfString;
  i: Integer;
  PortStr: String;
begin
  Result := False;
  PortStr := ':' + IntToStr(Port);
  TempFile := ExpandConstant('{tmp}\portcheck.txt');
  
  // Use netstat to check port
  Exec('cmd.exe', '/c netstat -an | findstr "' + PortStr + '" > "' + TempFile + '"', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  if LoadStringsFromFile(TempFile, Lines) then
  begin
    for i := 0 to GetArrayLength(Lines) - 1 do
    begin
      if Pos('LISTENING', Lines[i]) > 0 then
      begin
        Result := True;
        Break;
      end;
    end;
  end;
  
  DeleteFile(TempFile);
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

function GetPreviousInstallPath(): String;
var
  InstallPath: String;
begin
  Result := '';
  // Check registry for previous installation
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1',
                         'InstallLocation', InstallPath) then
    Result := InstallPath;
end;

function GetPreviousUninstallString(): String;
var
  UninstallString: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1',
                         'UninstallString', UninstallString) then
    Result := UninstallString;
end;

procedure StopAuraServices();
var
  ResultCode: Integer;
begin
  // Stop the tray application gracefully
  Exec('taskkill.exe', '/IM Aura.Tray.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Stop the AuraService Windows service
  Exec('sc.exe', 'stop AuraService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Give services time to fully stop
  Sleep(2000);
end;

function UninstallPreviousVersion(): Boolean;
var
  UninstallString: String;
  ResultCode: Integer;
begin
  Result := True;
  UninstallString := GetPreviousUninstallString();
  
  if UninstallString <> '' then
  begin
    // Remove quotes if present
    if (Length(UninstallString) > 0) and (UninstallString[1] = '"') then
      UninstallString := RemoveQuotes(UninstallString);
    
    // Run uninstaller silently, keeping user data
    // /VERYSILENT = no UI, /SUPPRESSMSGBOXES = no prompts
    // We do NOT pass /NORESTART since we're about to install anyway
    if not Exec(UninstallString, '/VERYSILENT /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      // Uninstall failed, but we can try to continue
      Log('Previous uninstall failed with code: ' + IntToStr(ResultCode));
    end;
    
    // Wait for uninstaller to complete file operations
    Sleep(1000);
  end;
end;

function InitializeSetup(): Boolean;
var
  PrevInstall: String;
begin
  Result := True;
  
  // Check for previous installation and handle upgrade
  PrevInstall := GetPreviousInstallPath();
  if PrevInstall <> '' then
  begin
    if MsgBox('A previous version of Aura is installed at:' + #13#10 +
              PrevInstall + #13#10 + #13#10 +
              'The installer will stop running services and upgrade the installation.' + #13#10 +
              'Your data and settings will be preserved.' + #13#10 + #13#10 +
              'Continue with upgrade?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
    
    // Stop services before uninstalling
    StopAuraServices();
    
    // Uninstall the previous version
    UninstallPreviousVersion();
  end;
  
  // Check for port conflicts (unless our services are already using them)
  if not AuraDBServiceExists() then
  begin
    Port5433InUse := IsPortInUse(5433);
    if Port5433InUse then
    begin
      if MsgBox('Port 5433 is already in use by another application.' + #13#10 +
                'This port is required for the Aura PostgreSQL database.' + #13#10 + #13#10 +
                'Please close the application using port 5433 and try again.' + #13#10 + #13#10 +
                'Continue anyway? (PostgreSQL setup may fail)', mbError, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;
  
  Port5300InUse := IsPortInUse(5300);
  if Port5300InUse then
  begin
    if MsgBox('Port 5300 is already in use by another application.' + #13#10 +
              'This port is required for the Aura API server.' + #13#10 + #13#10 +
              'Please close the application using port 5300 and try again.' + #13#10 + #13#10 +
              'Continue anyway? (Aura API may fail to start)', mbError, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
  
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

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;
  
  // Always stop services before copying files (handles upgrade case)
  // This runs right before file operations begin
  StopAuraServices();
  
  // Also delete the AuraService if it exists (we'll recreate it with correct settings)
  // This ensures service account changes are applied on upgrade
  Exec('sc.exe', 'delete AuraService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Give Windows time to release file handles
  Sleep(1000);
end;
