#!/bin/bash
set -e
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m'
header() { echo -e "\n${CYAN}=== $1 ===${NC}"; }
step() { echo -e "${GREEN}>> $1${NC}"; }

header "Checking macOS"
[[ "$(uname)" != "Darwin" ]] && echo "ERROR: macOS only" && exit 1
command -v brew &> /dev/null || { echo "Install Homebrew first"; exit 1; }

header "Installing OrbStack"
command -v orb &> /dev/null || { brew install --cask orbstack; open -a OrbStack; sleep 5; }

header "Installing PostgreSQL 16"
command -v psql &> /dev/null || { brew install postgresql@16; brew services start postgresql@16; }

header "Installing pgvector"
brew list pgvector &> /dev/null || brew install pgvector

header "Installing Ollama"
command -v ollama &> /dev/null || { brew install --cask ollama; open -a Ollama; sleep 5; }

header "Pulling Ollama Models"
for i in {1..30}; do curl -s http://localhost:11434/api/tags > /dev/null 2>&1 && break; sleep 1; done
ollama pull nomic-embed-text
ollama pull qwen2.5-coder:7b

header "Setting Up Database"
psql postgres -c "CREATE ROLE aura WITH LOGIN PASSWORD 'aura';" 2>/dev/null || true
psql -lqt | grep -qw aura || createdb -O aura aura
psql -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;" 2>/dev/null || true

header "Checking .NET SDK"
command -v dotnet &> /dev/null || brew install --cask dotnet-sdk

header "Building Aura"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
[ -d "$REPO_DIR/src/Aura.Api" ] && { cd "$REPO_DIR"; dotnet build; }

header "Done!"
echo "Run: dotnet run --project src/Aura.AppHost"
