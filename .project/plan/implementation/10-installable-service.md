# Phase 10: Installable Service & Tray Application

## Goal
Create a complete, end-user installable package that runs Aura on machines without .NET SDK.

## Prerequisites (User Must Have)
1. **Ollama** - Local LLM server (installer will check/prompt)
2. **PostgreSQL** - Database server (installer will check/prompt)
   - With pgvector extension installed

## Deliverables

### 10.1 Aura.Service Project
Windows Service that hosts the API, can run headless on boot.

**Files:**
- `src/Aura.Service/Aura.Service.csproj` - Self-contained executable settings
- `src/Aura.Service/Program.cs` - Windows Service host with Serilog
- `src/Aura.Service/appsettings.json` - Production configuration

**Key Features:**
- Uses `Microsoft.Extensions.Hosting.WindowsServices`
- Self-contained deployment (no .NET SDK required)
- Serilog logging to file + Windows Event Log
- Health endpoints exposed

### 10.2 Aura.Tray Project (Avalonia)
System tray application for service monitoring and control.

**Port from hve-hack:**
- `src/AgentOrchestrator.Tray/` → `src/Aura.Tray/`

**Files to create/port:**
- `src/Aura.Tray/Aura.Tray.csproj` - Avalonia cross-platform app
- `src/Aura.Tray/Program.cs` - Entry point
- `src/Aura.Tray/App.axaml` + `App.axaml.cs` - Avalonia app
- `src/Aura.Tray/TrayIconManager.cs` - Icon state management
- `src/Aura.Tray/ServiceMonitor.cs` - Health polling (API, Ollama, PostgreSQL, RAG)
- `src/Aura.Tray/StatusWindow.axaml` + `StatusWindow.axaml.cs` - Status popup
- `src/Aura.Tray/AutoStartManager.cs` - Windows startup registry
- `src/Aura.Tray/Assets/` - Tray icons (healthy, degraded, offline)

**Tray Menu:**
- Status indicator (green/yellow/red)
- Open Dashboard (browser)
- View Logs
- Start/Stop/Restart Service
- Settings
- Exit

### 10.3 Installer Package
Windows installer using Inno Setup (free, well-tested).

**Files:**
- `installers/windows/Aura.iss` - Inno Setup script
- `scripts/Build-Installer.ps1` - Build automation

**Installer Features:**
- Prerequisites check (Ollama, PostgreSQL)
- Install as Windows Service (optional)
- Start tray on Windows login (optional)
- Desktop shortcut (optional)
- Add to PATH
- Uninstall support

### 10.4 Build Scripts

**Scripts to create:**
- `scripts/Publish-Release.ps1` - Self-contained publish for all components
- `scripts/Build-Installer.ps1` - Create MSI/EXE installer

**Publish targets:**
- `publish/win-x64/Aura.Service/` - Service binaries
- `publish/win-x64/Aura.Tray/` - Tray app binaries
- `publish/win-x64/agents/` - Agent markdown files
- `publish/installers/Aura-Setup-{version}.exe` - Final installer

---

## Implementation Order

```
Step 10.1: Create Aura.Service (Windows Service host)
     │
     ├── 10.1.1 Create project file with self-contained settings
     ├── 10.1.2 Create Program.cs with Windows Service integration
     ├── 10.1.3 Create appsettings.json for production
     ├── 10.1.4 Add to solution
     └── 10.1.5 Test: dotnet run + sc.exe install
     │
Step 10.2: Port Aura.Tray (Avalonia tray app)
     │
     ├── 10.2.1 Create project file (Avalonia packages)
     ├── 10.2.2 Port Program.cs, App.axaml
     ├── 10.2.3 Port ServiceMonitor.cs (update URLs/names)
     ├── 10.2.4 Port TrayIconManager.cs
     ├── 10.2.5 Port StatusWindow.axaml
     ├── 10.2.6 Port AutoStartManager.cs
     ├── 10.2.7 Create/port tray icons (SVG assets)
     └── 10.2.8 Test: Build and run tray app
     │
Step 10.3: Create build scripts
     │
     ├── 10.3.1 scripts/Publish-Release.ps1
     └── 10.3.2 Verify self-contained publish works
     │
Step 10.4: Create installer
     │
     ├── 10.4.1 installers/windows/Aura.iss (Inno Setup)
     ├── 10.4.2 scripts/Build-Installer.ps1
     └── 10.4.3 Test: Full install/uninstall cycle
```

---

## Technical Details

### Self-Contained Publish Settings (csproj)
```xml
<PropertyGroup>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

### Windows Service Configuration
```csharp
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "AuraService";
});
```

### Default Ports
- Aura API: `http://localhost:5280`
- Ollama: `http://localhost:11434`
- PostgreSQL: `localhost:5432`

### Log Locations (Windows)
- Service: `%ProgramData%\Aura\logs\service.log`
- Tray: `%LocalAppData%\Aura\logs\tray.log`

### Database Connection String
```json
{
  "ConnectionStrings": {
    "AuraDb": "Host=localhost;Database=aura;Username=aura;Password=aura"
  }
}
```

---

## Dependencies to Add

**Aura.Service:**
- `Microsoft.Extensions.Hosting.WindowsServices`
- `Serilog.AspNetCore`
- `Serilog.Sinks.File`
- `Serilog.Sinks.EventLog`

**Aura.Tray:**
- `Avalonia` (11.2.x)
- `Avalonia.Desktop`
- `Avalonia.Themes.Fluent`

**Build Tools (dev machine only):**
- Inno Setup 6.x (`winget install -e --id JRSoftware.InnoSetup`)

---

## Validation Checklist

- [ ] `dotnet publish` creates self-contained binaries
- [ ] Service runs without .NET SDK installed
- [ ] Service starts on Windows boot
- [ ] Tray app shows correct status
- [ ] Tray can start/stop service
- [ ] Installer runs on clean Windows
- [ ] Uninstaller removes all components
- [ ] Logs are written to correct locations
