# Build and Install VS Code Extension

Build the Aura VS Code extension and install it.

## Command

```powershell
c:\work\aura\scripts\Build-Extension.ps1
```

This script will:
1. Compile the TypeScript extension in `c:\work\aura\extension`
2. Package it as a VSIX file
3. Install the extension in VS Code

After installation, reload VS Code (Ctrl+Shift+P → "Developer: Reload Window") to use the updated extension.
