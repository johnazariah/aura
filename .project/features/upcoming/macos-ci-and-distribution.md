# macOS CI and Distribution

**Status:** 🔜 Planned
**Priority:** Low
**Effort:** Medium (3-5 days)
**Blocked By:** Self-hosted macOS runner or GHA budget for macOS minutes

## Summary

Complete macOS support with CI builds, Homebrew distribution, and a native menu bar app. This is the follow-up to [macos-local-development.md](../completed/macos-local-development.md) which enabled local development.

## Current State

- ✅ Code compiles and runs on macOS (local development works)
- ✅ TreeSitter.DotNet 1.2.0 includes macOS native libraries
- ✅ Installation script and docs for local setup
- ❌ No macOS CI builds (GitHub macOS runners cost 10x)
- ❌ No Homebrew cask for distribution
- ❌ No macOS menu bar app (Tray equivalent)
- ❌ No launchd service configuration

## Requirements

### 1. macOS CI Builds

**Problem**: GitHub's macOS runners cost 10x Linux minutes.

**Options**:

1. Use self-hosted macOS runner (Mac Studio, Mac Mini)
2. Use cheaper macOS cloud providers (MacStadium, AWS EC2 Mac)
3. Run macOS builds only on releases, not every PR

**Recommendation**: Self-hosted runner on Mac Studio for daily development.

### 2. Homebrew Distribution

Create a Homebrew cask for easy installation:

```ruby
cask "aura" do
  version "1.0.0"
  sha256 "..."
  url "https://github.com/johnazariah/aura/releases/download/v#{version}/Aura-#{version}-macos.zip"
  name "Aura"
  homepage "https://github.com/johnazariah/aura"
  
  depends_on formula: "postgresql@16"
  depends_on cask: "ollama"
  
  app "Aura.app"
  # ... launchd service, etc.
end
```

### 3. macOS Menu Bar App

Port Aura.Tray to macOS using:
- Avalonia (cross-platform, already used for Windows)
- Or native Swift/AppKit for better macOS integration

**Components**:
- Menu bar icon with status indicator
- Dropdown showing service health
- Quick actions (start/stop, open logs)

### 4. launchd Service

Create a launchd plist for the Aura API:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "...">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.aura.api</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/aura-api</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
```

## Implementation Plan

### Phase 1: Self-Hosted Runner (1 day)

1. Set up GitHub Actions runner on Mac Studio
2. Add macOS to CI matrix (self-hosted only)
3. Verify unit tests pass

### Phase 2: Menu Bar App (2-3 days)

1. Test Avalonia Aura.Tray on macOS
2. Fix any platform-specific issues
3. Create macOS app bundle (.app)

### Phase 3: Distribution (1-2 days)

1. Create Homebrew tap repository
2. Write cask formula
3. Create launchd plist
4. Test full installation flow

## Acceptance Criteria

- [ ] macOS CI builds on self-hosted runner
- [ ] Unit tests pass on macOS in CI
- [ ] Homebrew cask installs Aura successfully
- [ ] Aura API starts as launchd service
- [ ] Menu bar app shows status
- [ ] VS Code extension connects to local API

## Dependencies

- Self-hosted macOS runner (Mac Studio)
- Homebrew tap repository
- Apple Developer certificate (optional, for code signing)

## Related

- [macos-local-development.md](../completed/macos-local-development.md) - Completed local development support
- [install-mac.sh](../../../setup/install-mac.sh) - Current installation script
- [Bundled Extension](../completed/bundled-extension.md) - Windows extension bundling pattern
