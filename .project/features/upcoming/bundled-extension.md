# Bundled VS Code Extension

**Status:** 🔲 Not Started  
**Effort:** 2-3 hours  
**Priority:** P0 - Required for Distribution

## Summary

Bundle the VS Code extension (`.vsix`) with the Windows installer and automatically install it during setup.

## Current State

- Extension built separately via `Build-Extension.ps1`
- Produces `extension/aura-X.Y.Z.vsix`
- User must manually install via `code --install-extension`
- Not integrated with Windows installer

## Goals

1. Include VSIX in Windows installer
2. Auto-install extension during setup (if VS Code detected)
3. Provide manual install option if VS Code not found

## Implementation

### 1. Update Publish Script

**File:** `scripts/Publish-Release.ps1`

Add extension build:

```powershell
# Build VS Code extension
Write-Host "`nBuilding VS Code Extension..." -ForegroundColor Green
Push-Location extension
npm ci
npm run package
Pop-Location

# Copy VSIX to publish folder
Copy-Item "extension/aura-*.vsix" "$OutputDir/win-x64/extension/"
```

### 2. Update Installer

**File:** `installers/windows/Aura.iss`

```inno
[Files]
; VS Code Extension
Source: "..\..\publish\win-x64\extension\*.vsix"; DestDir: "{app}\extension"; Flags: ignoreversion

[Tasks]
Name: "installextension"; Description: "Install VS Code extension"; GroupDescription: "VS Code Integration:"; Flags: checked; Check: VSCodeExists

[Run]
; Install VS Code extension
Filename: "{code:GetVSCodePath}"; Parameters: "--install-extension ""{app}\extension\aura-{#MyAppVersion}.vsix"" --force"; Flags: runhidden nowait; Tasks: installextension

[Code]
var
  VSCodePath: string;

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
  
  // Check for VS Code
  if not VSCodeExists() then
  begin
    MsgBox('VS Code was not detected. The Aura extension will be placed in the installation folder.' + #13#10 + #13#10 +
           'You can manually install it later with:' + #13#10 +
           'code --install-extension "' + ExpandConstant('{app}') + '\extension\aura-{#MyAppVersion}.vsix"',
           mbInformation, MB_OK);
  end;
  
  // Existing Ollama check...
end;
```

### 3. Add Manual Install Script

**File:** `installers/windows/install-extension.ps1` (bundled with installer)

```powershell
# Manual VS Code extension installation
$vsixPath = Join-Path $PSScriptRoot "..\extension\aura-*.vsix"
$vsix = Get-Item $vsixPath | Select-Object -First 1

if (-not $vsix) {
    Write-Error "Extension not found at $vsixPath"
    exit 1
}

# Try to find VS Code
$codePaths = @(
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd",
    "C:\Program Files\Microsoft VS Code\bin\code.cmd",
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code Insiders\bin\code-insiders.cmd"
)

$code = $codePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $code) {
    Write-Host "VS Code not found. Please install manually:" -ForegroundColor Yellow
    Write-Host "  code --install-extension `"$($vsix.FullName)`"" -ForegroundColor Cyan
    exit 1
}

Write-Host "Installing Aura extension..." -ForegroundColor Green
& $code --install-extension $vsix.FullName --force

Write-Host "Done! Restart VS Code to activate the extension." -ForegroundColor Green
```

Add to installer:

```inno
[Files]
Source: "install-extension.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
```

### 4. Version Synchronization

Ensure extension version matches installer version:

**File:** `scripts/Publish-Release.ps1`

```powershell
# Update extension version to match
$packageJson = Get-Content "extension/package.json" | ConvertFrom-Json
$packageJson.version = $Version
$packageJson | ConvertTo-Json -Depth 10 | Set-Content "extension/package.json"
```

## Folder Structure After Install

```
C:\Program Files\Aura\
├── api\
│   └── Aura.Api.exe
├── tray\
│   └── Aura.Tray.exe
├── agents\
│   └── *.md
├── extension\
│   └── aura-1.0.0.vsix
├── scripts\
│   └── install-extension.ps1
└── version.json
```

## Tasks

- [ ] Update `Publish-Release.ps1` to build and copy VSIX
- [ ] Update `Aura.iss` with VS Code detection and install
- [ ] Create `install-extension.ps1` helper script
- [ ] Add version sync between installer and extension
- [ ] Test on machine with/without VS Code
- [ ] Test with VS Code Insiders

## Dependencies

- GitHub Release Automation spec (needs VSIX in release artifacts)
