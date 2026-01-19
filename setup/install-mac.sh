#!/bin/bash
# Install Aura and all prerequisites on macOS
# Usage: ./setup/install-mac.sh

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

header() { echo -e "\n${CYAN}============================================================${NC}"; echo -e "${CYAN} $1${NC}"; echo -e "${CYAN}============================================================${NC}"; }
step() { echo -e "${GREEN}>> $1${NC}"; }
skip() { echo -e "${GRAY}-- $1 (already installed)${NC}"; }

# =============================================================================
# Check macOS
# =============================================================================
header "Checking System Requirements"

if [[ "$(uname)" != "Darwin" ]]; then
    echo -e "${RED}ERROR: This script is for macOS only${NC}"
    exit 1
fi
step "macOS detected: $(sw_vers -productVersion)"

if ! command -v brew &> /dev/null; then
    echo -e "${RED}ERROR: Homebrew not found. Install from https://brew.sh${NC}"
    exit 1
fi
step "Homebrew available"

# =============================================================================
# Install OrbStack (Docker-compatible container runtime)
# =============================================================================
header "Installing OrbStack"

if command -v orb &> /dev/null; then
    skip "OrbStack"
else
    step "Installing OrbStack..."
    brew install --cask orbstack
    step "Starting OrbStack..."
    open -a OrbStack
    sleep 5
fi

# =============================================================================
# Install PostgreSQL 16
# =============================================================================
header "Installing PostgreSQL 16"

if command -v psql &> /dev/null; then
    skip "PostgreSQL"
else
    step "Installing PostgreSQL 16..."
    brew install postgresql@16
    step "Starting PostgreSQL service..."
    brew services start postgresql@16
    sleep 3
fi

# =============================================================================
# Install pgvector
# =============================================================================
header "Installing pgvector"

if brew list pgvector &> /dev/null 2>&1; then
    skip "pgvector"
else
    step "Installing pgvector..."
    brew install pgvector
fi

# =============================================================================
# Install Ollama
# =============================================================================
header "Installing Ollama"

if command -v ollama &> /dev/null; then
    skip "Ollama"
else
    step "Installing Ollama..."
    brew install --cask ollama
    step "Starting Ollama..."
    open -a Ollama
    sleep 5
fi

# =============================================================================
# Pull Ollama Models
# =============================================================================
header "Pulling Ollama Models"

step "Waiting for Ollama..."
for i in {1..30}; do
    if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        break
    fi
    sleep 1
done

step "Pulling nomic-embed-text (embeddings)..."
ollama pull nomic-embed-text

step "Pulling qwen2.5-coder:7b (code generation)..."
ollama pull qwen2.5-coder:7b

# =============================================================================
# Set Up Database
# =============================================================================
header "Setting Up Database"

step "Creating aura role..."
psql postgres -c "CREATE ROLE aura WITH LOGIN PASSWORD 'aura';" 2>/dev/null || true

step "Creating aura database..."
if ! psql -lqt | grep -qw aura; then
    createdb -O aura aura
else
    skip "aura database"
fi

step "Enabling pgvector extension..."
psql -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;" 2>/dev/null || true

# =============================================================================
# Check .NET SDK
# =============================================================================
header "Checking .NET SDK"

if command -v dotnet &> /dev/null; then
    version=$(dotnet --version)
    skip ".NET SDK: $version"
else
    step "Installing .NET SDK..."
    brew install --cask dotnet-sdk
fi

# =============================================================================
# Build Aura
# =============================================================================
header "Building Aura"

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

if [ -d "$REPO_DIR/src/Aura.Api" ]; then
    step "Building Aura from source..."
    cd "$REPO_DIR"
    dotnet build
    step "Build complete"
else
    echo -e "${GRAY}  Clone the Aura repository to build from source${NC}"
fi

# =============================================================================
# Summary
# =============================================================================
header "Installation Complete!"

echo ""
echo -e "Installed components:"
echo -e "${GRAY}  - OrbStack (Docker-compatible container runtime)${NC}"
echo -e "${GRAY}  - PostgreSQL 16 with pgvector (native via Homebrew)${NC}"
echo -e "${GRAY}  - Ollama (local LLM)${NC}"
echo -e "${GRAY}  - nomic-embed-text model${NC}"
echo -e "${GRAY}  - qwen2.5-coder:7b model${NC}"
echo -e "${GRAY}  - .NET SDK${NC}"
echo ""
echo -e "Next steps:"
echo -e "${GRAY}  1. cd $REPO_DIR${NC}"
echo -e "${GRAY}  2. dotnet run --project src/Aura.AppHost${NC}"
echo -e "${GRAY}  3. API available at: http://localhost:5280${NC}"
echo ""
