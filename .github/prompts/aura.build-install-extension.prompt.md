# Build and Install Aura VS Code Extension

Build the Aura VS Code extension as a VSIX package and install it locally.

## Prerequisites
- Node.js 18+ installed
- VS Code installed
- `vsce` CLI (will be installed if missing)

## Steps

### 1. Install dependencies (if not already done)
```powershell
cd c:\work\aura\extension
npm install
```

### 2. Install vsce globally (VS Code Extension CLI)
```powershell
npm install -g @vscode/vsce
```

### 3. Compile the extension
```powershell
npm run compile
```

### 4. Package as VSIX
```powershell
vsce package --no-dependencies
```

This creates `aura-0.1.0.vsix` in the extension folder.

### 5. Install the VSIX in VS Code

**Option A: Via command line**
```powershell
code --install-extension aura-0.1.0.vsix
```

**Option B: Via VS Code UI**
1. Open VS Code
2. Press `Ctrl+Shift+P` → "Extensions: Install from VSIX..."
3. Select the generated `.vsix` file

### 6. Reload VS Code
Press `Ctrl+Shift+P` → "Developer: Reload Window"

## One-liner (build + install)
```powershell
cd c:\work\aura\extension; npm run compile; vsce package --no-dependencies; code --install-extension aura-0.1.0.vsix
```

## Verification
After installation:
1. Look for the **Aura** icon in the Activity Bar (left sidebar)
2. Click it to see the **System Status** and **Agents** panels
3. Status will show as "unhealthy" until the API server is running

## Development Mode (alternative)
Instead of packaging, you can run in development mode:
```powershell
cd c:\work\aura\extension
npm run watch
```
Then press `F5` in VS Code to launch Extension Development Host.

## Notes
- The extension connects to `http://localhost:5300` by default
- Configure via Settings → Extensions → Aura
- Auto-refresh polls every 10 seconds (can be disabled in settings)
