# Setup Instructions

> **Purpose**: Bootstrap a consistent development environment using a devcontainer. Run this prompt once when cloning the repository or onboarding a new teammate.

## Your Task

Create a `.devcontainer/` configuration that provides a reproducible development environment.

## Step 1: Detect Project Type

First, determine what the project needs:

### If `Makefile` exists:

1. Read the `Makefile` and identify:
   - Package manager commands (`uv`, `npm`, `pip`, `poetry`, etc.)
   - Runtime requirements (`python`, `node`, `go`, etc.)
   - Linting tools (`ruff`, `eslint`, `golangci-lint`, etc.)
   - Test runners (`pytest`, `jest`, `go test`, etc.)

2. Extract version requirements from:
   - `pyproject.toml` → `requires-python`
   - `package.json` → `engines.node`
   - `go.mod` → `go` directive
   - Or similar language-specific files

### If NO `Makefile` exists:

Ask the user:
```
No Makefile detected. Which language/framework would you like to set up?

1. Python (with uv)
2. Python (with pip)
3. Node.js (with npm)
4. Node.js (with pnpm)
5. Go
6. Rust
7. Other (specify)
```

Then generate an appropriate basic devcontainer for their choice.

---

## Step 2: Generate Devcontainer Files

Create the following structure:

```
.devcontainer/
├── devcontainer.json    # Main configuration
├── Dockerfile           # (if custom image needed)
└── post-create.sh       # Setup script run after container creation
```

### devcontainer.json Template

```json
{
  "name": "{project-name} Dev Container",
  "build": {
    "dockerfile": "Dockerfile",
    "context": ".."
  },
  "features": {
    // Add detected features here
  },
  "customizations": {
    "vscode": {
      "extensions": [
        // Add language-appropriate extensions
      ],
      "settings": {
        // Add language-appropriate settings
      }
    }
  },
  "postCreateCommand": "bash .devcontainer/post-create.sh",
  "remoteUser": "vscode"
}
```

---

## Step 3: Language-Specific Configurations

### Python (with uv) - This Repository

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/devcontainers/python:3.12

# Install uv
RUN curl -LsSf https://astral.sh/uv/install.sh | sh
ENV PATH="/root/.local/bin:$PATH"

# Install any system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    && rm -rf /var/lib/apt/lists/*
```

**devcontainer.json:**
```json
{
  "name": "Python UV Dev Container",
  "build": {
    "dockerfile": "Dockerfile",
    "context": ".."
  },
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-python.python",
        "ms-python.vscode-pylance",
        "charliermarsh.ruff",
        "tamasfe.even-better-toml"
      ],
      "settings": {
        "python.defaultInterpreterPath": ".venv/bin/python",
        "python.testing.pytestEnabled": true,
        "python.testing.pytestArgs": ["tests"],
        "[python]": {
          "editor.defaultFormatter": "charliermarsh.ruff",
          "editor.formatOnSave": true
        }
      }
    }
  },
  "postCreateCommand": "bash .devcontainer/post-create.sh",
  "remoteUser": "vscode"
}
```

**post-create.sh:**
```bash
#!/bin/bash
set -e

echo "🔧 Setting up development environment..."

# Ensure uv is available
export PATH="$HOME/.local/bin:$PATH"

# Create virtual environment first
if [ -f "pyproject.toml" ]; then
    echo "🐍 Creating virtual environment..."
    uv venv .venv
    echo "✅ Virtual environment created at .venv/"
fi

# Install dependencies
if [ -f "Makefile" ]; then
    echo "📦 Running make install..."
    make install
elif [ -f "pyproject.toml" ]; then
    echo "📦 Installing with uv..."
    uv sync --all-extras
fi

echo "✅ Development environment ready!"
echo "💡 Activate with: source .venv/bin/activate (or .venv\\Scripts\\Activate.ps1 on Windows)"
```

### Node.js (with npm)

**devcontainer.json:**
```json
{
  "name": "Node.js Dev Container",
  "image": "mcr.microsoft.com/devcontainers/javascript-node:20",
  "customizations": {
    "vscode": {
      "extensions": [
        "dbaeumer.vscode-eslint",
        "esbenp.prettier-vscode"
      ]
    }
  },
  "postCreateCommand": "npm install"
}
```

### Go

**devcontainer.json:**
```json
{
  "name": "Go Dev Container",
  "image": "mcr.microsoft.com/devcontainers/go:1.22",
  "customizations": {
    "vscode": {
      "extensions": [
        "golang.go"
      ]
    }
  },
  "postCreateCommand": "go mod download"
}
```

---

## Step 4: Validate Configuration

After generating files:

1. Verify `devcontainer.json` is valid JSON
2. Verify Dockerfile builds successfully (if created)
3. Verify post-create script is executable
4. List the files created

---

## Output

Report what was created:

```
✅ Devcontainer created:
   - .devcontainer/devcontainer.json
   - .devcontainer/Dockerfile
   - .devcontainer/post-create.sh

Detected:
   - Language: Python 3.10+
   - Package Manager: uv
   - Test Runner: pytest
   - Linter: ruff

To use:
   1. Open VS Code
   2. Cmd/Ctrl+Shift+P → "Dev Containers: Reopen in Container"
   3. Wait for container to build and post-create script to run
   4. Ready to develop!
```

---

## Notes

- Devcontainer should be **self-contained** - new teammates should only need Docker and VS Code
- Post-create script should be **idempotent** - safe to run multiple times
- Prefer official Microsoft devcontainer base images when available
- Include only **essential** VS Code extensions for the language
