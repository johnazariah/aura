# Tool Prerequisites

This document lists all external tools that must be installed on the developer machine for the Aura language-specific coding agents to function properly.

## Overview

Aura's language-specific coding agents rely on external command-line tools rather than embedding language compilers directly. This approach:

- **Reduces complexity** - No need to keep up with compiler service API changes
- **Leverages existing tools** - Use battle-tested tooling that developers already know
- **Supports latest versions** - Always uses whatever version is installed on the machine
- **Maintains consistency** - Same tools developers use manually

---

## Core Requirements

These tools are required for basic Aura functionality:

| Tool | Minimum Version | Purpose | Installation |
|------|-----------------|---------|--------------|
| **.NET SDK** | 8.0+ | Build C#/F# projects, run tests | [Download](https://dotnet.microsoft.com/download) |
| **Git** | 2.30+ | Version control, worktree operations | [Download](https://git-scm.com/downloads) |

---

## C# / .NET (Roslyn Coding Agent)

The C# agent uses the .NET SDK and Roslyn analyzers embedded in the Aura.Module.Developer project.

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **dotnet** | ✅ | Build, test, run | Included with .NET SDK |

### Verification

```powershell
dotnet --version
# Should output: 8.0.x or higher
```

---

## F# (F# Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **dotnet** | ✅ | Build F# projects, run tests | Included with .NET SDK |
| **fantomas** | ✅ | Code formatting | `dotnet tool install -g fantomas` |
| **fsharplint** | Optional | Linting (TODO: not yet implemented) | `dotnet tool install -g dotnet-fsharplint` |

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `fsharp.check_project` | `dotnet build --no-restore` | Type-check F# project without full build |
| `fsharp.build` | `dotnet build` | Build F# project |
| `fsharp.format` | `fantomas` | Format F# source files |
| `fsharp.test` | `dotnet test` | Run F# unit tests |
| `fsharp.fsi` | `dotnet fsi` | Run F# script in FSI |

### Verification

```powershell
# Check .NET SDK
dotnet --version

# Check Fantomas (install if missing)
fantomas --version
# If not found: dotnet tool install -g fantomas

# Ensure global tools are in PATH
$env:PATH -split ';' | Where-Object { $_ -like '*\.dotnet\tools*' }
```

---

## Python (Python Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **python** | ✅ | Run Python scripts | [Download](https://www.python.org/downloads/) |
| **pip** | ✅ | Package management | Included with Python |
| **ruff** | ✅ | Linting and formatting | `pip install ruff` |
| **pytest** | ✅ | Testing | `pip install pytest` |
| **mypy** | Optional | Static type checking | `pip install mypy` |

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `python.run_script` | `python` | Execute Python script |
| `python.run_tests` | `python -m pytest` | Run tests with pytest |
| `python.lint` | `ruff check` | Lint Python files |
| `python.format` | `ruff format` | Format Python files |
| `python.type_check` | `mypy` | Static type checking |

### Verification

```powershell
python --version    # 3.10+ recommended
pip --version
ruff --version      # pip install ruff
pytest --version    # pip install pytest
mypy --version      # pip install mypy (optional)
```

### Quick Setup

```powershell
pip install ruff pytest mypy
```

---

## TypeScript (TypeScript Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **node** | ✅ | JavaScript runtime | [Download](https://nodejs.org/) |
| **npm** | ✅ | Package management | Included with Node.js |
| **tsc** | ✅ | TypeScript compiler | `npm install -g typescript` |
| **eslint** | ✅ | Linting | `npm install -g eslint` |
| **prettier** | ✅ | Formatting | `npm install -g prettier` |
| **jest** | Optional | Testing | `npm install -g jest` (or use project-local) |

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `typescript.compile` | `tsc` | Compile TypeScript |
| `typescript.type_check` | `tsc --noEmit` | Type-check without emit |
| `typescript.run_tests` | `npm test` or `jest` | Run tests |
| `typescript.lint` | `eslint` | Lint files |
| `typescript.format` | `prettier --write` | Format files |

### Verification

```powershell
node --version      # 18+ recommended
npm --version
tsc --version       # npm install -g typescript
eslint --version    # npm install -g eslint
prettier --version  # npm install -g prettier
```

### Quick Setup

```powershell
npm install -g typescript eslint prettier
```

---

## Go (Go Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **go** | ✅ | Go compiler and toolchain | [Download](https://go.dev/dl/) |
| **goimports** | ✅ | Import organization | `go install golang.org/x/tools/cmd/goimports@latest` |
| **golangci-lint** | ✅ | Linting | [Installation](https://golangci-lint.run/usage/install/) |

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `go.build` | `go build` | Build Go package/project |
| `go.test` | `go test` | Run tests |
| `go.vet` | `go vet` | Report suspicious constructs |
| `go.fmt` | `goimports` | Format and organize imports |
| `go.mod_tidy` | `go mod tidy` | Clean up go.mod/go.sum |

### Verification

```powershell
go version          # 1.21+ recommended
goimports -h        # go install golang.org/x/tools/cmd/goimports@latest
golangci-lint --version  # See installation docs
```

### Quick Setup

```powershell
go install golang.org/x/tools/cmd/goimports@latest

# golangci-lint (Windows)
# Download from https://github.com/golangci/golangci-lint/releases
# Or use: go install github.com/golangci/golangci-lint/cmd/golangci-lint@latest
```

---

## Elm (Elm Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **elm** | ✅ | Elm compiler | [Download](https://guide.elm-lang.org/install/elm.html) |
| **elm-format** | ✅ | Code formatting | `npm install -g elm-format` |
| **elm-test** | ✅ | Testing | `npm install -g elm-test` |
| **elm-review** | Optional | Linting | `npm install -g elm-review` |

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `elm.build` | `elm make` | Compile Elm to JavaScript |
| `elm.format` | `elm-format` | Format Elm code |
| `elm.test` | `elm-test` | Run Elm tests |
| `elm.review` | `elm-review` | Lint Elm code |
| `elm.repl` | `elm repl` | Interactive REPL |

### Verification

```powershell
elm --version           # 0.19.1 recommended
elm-format --version    # npm install -g elm-format
elm-test --version      # npm install -g elm-test
elm-review --version    # npm install -g elm-review (optional)
```

### Quick Setup

```powershell
# Install Elm (via npm or installer)
npm install -g elm elm-format elm-test

# Optional: elm-review for linting
npm install -g elm-review
```

---

## Haskell (Haskell Coding Agent)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| **stack** | ✅* | Build tool | [Download](https://docs.haskellstack.org/en/stable/) |
| **cabal** | ✅* | Alternative build tool | [GHCup](https://www.haskell.org/ghcup/) |
| **ghc** | ✅ | Haskell compiler | Installed via Stack or GHCup |
| **ormolu** | ✅ | Code formatting | `stack install ormolu` |
| **hlint** | ✅ | Linting | `stack install hlint` |

*Either Stack or Cabal is required. Stack is recommended for beginners.

### Tools Used

| Tool Name | Command | Description |
|-----------|---------|-------------|
| `haskell.build` | `stack build` | Build Haskell project |
| `haskell.test` | `stack test` | Run tests |
| `haskell.typecheck` | `stack build --fast` | Fast type-check |
| `haskell.format` | `ormolu` | Format Haskell code |
| `haskell.lint` | `hlint` | Lint Haskell code |
| `haskell.ghci` | `stack ghci` | Interactive GHCi REPL |

### Verification

```powershell
stack --version         # Install via haskellstack.org
ghc --version           # Installed via stack or ghcup
ormolu --version        # stack install ormolu
hlint --version         # stack install hlint
```

### Quick Setup (Stack - Recommended)

```powershell
# Install Stack (manages GHC automatically)
# Windows: Download installer from haskellstack.org
# macOS: brew install haskell-stack
# Linux: curl -sSL https://get.haskellstack.org/ | sh

# Install formatters and linters
stack install ormolu hlint
```

### Quick Setup (GHCup - Alternative)

```powershell
# Install GHCup (manages GHC, Cabal, HLS)
# See: https://www.haskell.org/ghcup/

ghcup install ghc
ghcup install cabal
ghcup install hls       # Haskell Language Server

# Install tools via Cabal
cabal install ormolu hlint
```

---

## Environment Variables

Ensure these paths are in your system PATH:

| Platform | Paths to Add |
|----------|--------------|
| **Windows** | `%USERPROFILE%\.dotnet\tools`, `%GOPATH%\bin`, `%APPDATA%\npm` |
| **macOS/Linux** | `~/.dotnet/tools`, `$GOPATH/bin`, `~/.npm-global/bin` |

### Windows PATH Setup (PowerShell)

```powershell
# Check current PATH entries for tools
$env:PATH -split ';' | Where-Object { 
    $_ -like '*dotnet*' -or 
    $_ -like '*go*' -or 
    $_ -like '*npm*' -or
    $_ -like '*Python*'
}

# Add .NET global tools to current session
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

---

## Docker/Container Runtime

For running integration tests and Aspire-based development:

| Platform | Recommended Runtime |
|----------|---------------------|
| **Windows** | [Podman](https://podman.io/docs/installation) or Docker Desktop |
| **macOS** | [OrbStack](https://orbstack.dev/) or Docker Desktop |
| **Linux** | Podman or Docker |

Aspire works with any Docker-compatible runtime.

---

## Verification Script

Run this PowerShell script to check all prerequisites:

```powershell
$tools = @(
    @{ Name = "dotnet"; Command = "dotnet --version"; Required = $true },
    @{ Name = "git"; Command = "git --version"; Required = $true },
    @{ Name = "fantomas"; Command = "fantomas --version"; Required = $false },
    @{ Name = "python"; Command = "python --version"; Required = $false },
    @{ Name = "ruff"; Command = "ruff --version"; Required = $false },
    @{ Name = "pytest"; Command = "pytest --version"; Required = $false },
    @{ Name = "node"; Command = "node --version"; Required = $false },
    @{ Name = "tsc"; Command = "tsc --version"; Required = $false },
    @{ Name = "eslint"; Command = "eslint --version"; Required = $false },
    @{ Name = "prettier"; Command = "prettier --version"; Required = $false },
    @{ Name = "go"; Command = "go version"; Required = $false },
    @{ Name = "goimports"; Command = "goimports -h 2>&1 | Select-Object -First 1"; Required = $false },
    @{ Name = "golangci-lint"; Command = "golangci-lint --version"; Required = $false },
    @{ Name = "elm"; Command = "elm --version"; Required = $false },
    @{ Name = "elm-format"; Command = "elm-format --version"; Required = $false },
    @{ Name = "elm-test"; Command = "elm-test --version"; Required = $false },
    @{ Name = "stack"; Command = "stack --version"; Required = $false },
    @{ Name = "ghc"; Command = "ghc --version"; Required = $false },
    @{ Name = "ormolu"; Command = "ormolu --version"; Required = $false },
    @{ Name = "hlint"; Command = "hlint --version"; Required = $false }
)

foreach ($tool in $tools) {
    try {
        $result = Invoke-Expression $tool.Command 2>&1
        $status = if ($result) { "✅" } else { "❌" }
        Write-Host "$status $($tool.Name): $result"
    }
    catch {
        $icon = if ($tool.Required) { "❌" } else { "⚠️" }
        Write-Host "$icon $($tool.Name): Not found"
    }
}
```

---

## Adding New Language Support

See [Process: Adding a Specialist Coding Agent](../.project/processes/add-specialist-coding-agent.md) for detailed instructions.

**Quick summary:**

1. **For simple CLI-based languages** (recommended):
   - Create `agents/languages/{language}.yaml` with tool definitions
   - The ConfigurableLanguageAgent will handle the rest

2. **For languages needing custom code**:
   - Create `{Language}Tools.cs` in `Aura.Module.Developer/Tools/`
   - Create `{Language}CodingAgent.cs` in `Aura.Module.Developer/Agents/`
   - Create `{language}-coding.prompt` in `prompts/`
   - Register in `DeveloperModule.cs` and `DeveloperAgentProvider.cs`

3. **Document prerequisites in this file**

---

## Troubleshooting

### "Command not found" errors

1. Check if the tool is installed: `Get-Command <tool>`
2. Verify PATH includes the tool's directory
3. Restart your terminal after installing new tools

### Tool version mismatches

If a tool's output format changed, the Aura tool wrapper may need updating. Check the tool's changelog and update the corresponding `*Tools.cs` parser.

### Permission errors

- **Windows**: Run terminal as Administrator for global tool installation
- **macOS/Linux**: Use `sudo` for system-wide installation, or prefer user-local installs

### Fantomas not formatting

1. Ensure `.editorconfig` exists in the project root
2. Check Fantomas version compatibility: `fantomas --version`
3. Try formatting directly: `fantomas <file.fs> --check`
