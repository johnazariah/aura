#!/bin/bash
# Install Aura as a native macOS service (no containers)
# Usage: ./installers/macos/install-service.sh

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[0;33m'
GRAY='\033[0;90m'
NC='\033[0m'

header() { echo -e "\n${CYAN}============================================================${NC}"; echo -e "${CYAN} $1${NC}"; echo -e "${CYAN}============================================================${NC}"; }
step() { echo -e "${GREEN}>> $1${NC}"; }
skip() { echo -e "${GRAY}-- $1 (already done)${NC}"; }
warn() { echo -e "${YELLOW}!! $1${NC}"; }

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
INSTALL_DIR="/usr/local/share/aura"
LOG_DIR="/usr/local/var/log/aura"
PLIST_SRC="$SCRIPT_DIR/com.aura.api.plist"
PLIST_DST="$HOME/Library/LaunchAgents/com.aura.api.plist"

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
# Install PostgreSQL 17 with pgvector
# =============================================================================
header "Installing PostgreSQL 17"

if command -v /opt/homebrew/opt/postgresql@17/bin/psql &> /dev/null; then
    skip "PostgreSQL 17 already installed"
else
    step "Installing PostgreSQL 17..."
    brew install postgresql@17
fi

if brew list pgvector &> /dev/null 2>&1; then
    skip "pgvector already installed"
else
    step "Installing pgvector..."
    brew install pgvector
fi

# Start PostgreSQL service
step "Starting PostgreSQL service..."
brew services start postgresql@17 2>/dev/null || true

# Wait for PostgreSQL to be ready
step "Waiting for PostgreSQL to start..."
for i in {1..30}; do
    if /opt/homebrew/opt/postgresql@17/bin/pg_isready -q 2>/dev/null; then
        break
    fi
    sleep 1
done

# =============================================================================
# Set Up Database
# =============================================================================
header "Setting Up Database"

export PATH="/opt/homebrew/opt/postgresql@17/bin:$PATH"

# Create role
step "Creating aura role..."
psql postgres -c "CREATE ROLE aura WITH LOGIN PASSWORD 'aura';" 2>/dev/null || skip "Role 'aura' already exists"

# Create database
if ! psql -lqt | cut -d \| -f 1 | grep -qw aura; then
    step "Creating aura database..."
    createdb -O aura aura
else
    skip "Database 'aura' already exists"
fi

# Enable pgvector extension
step "Enabling pgvector extension..."
psql -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;" 2>/dev/null || true

# =============================================================================
# Install Ollama
# =============================================================================
header "Installing Ollama"

if command -v ollama &> /dev/null; then
    skip "Ollama already installed"
else
    step "Installing Ollama..."
    brew install --cask ollama
fi

# Start Ollama if not running
if ! curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
    step "Starting Ollama..."
    open -a Ollama
    
    step "Waiting for Ollama..."
    for i in {1..30}; do
        if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
            break
        fi
        sleep 1
    done
fi

# Pull models if not present
if ! ollama list 2>/dev/null | grep -q "nomic-embed-text"; then
    step "Pulling nomic-embed-text model..."
    ollama pull nomic-embed-text
else
    skip "nomic-embed-text model already installed"
fi

if ! ollama list 2>/dev/null | grep -q "qwen2.5-coder:7b"; then
    step "Pulling qwen2.5-coder:7b model..."
    ollama pull qwen2.5-coder:7b
else
    skip "qwen2.5-coder:7b model already installed"
fi

# =============================================================================
# Install .NET SDK
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
# Install Aura Files
# =============================================================================
header "Installing Aura"

step "Creating directories..."
sudo mkdir -p "$INSTALL_DIR"
sudo mkdir -p "$LOG_DIR"
sudo chown -R $(whoami) "$INSTALL_DIR" "$LOG_DIR"

step "Copying Aura files..."
rsync -a --exclude 'bin' --exclude 'obj' --exclude '.git' --exclude 'node_modules' \
    "$REPO_DIR/" "$INSTALL_DIR/"

step "Building Aura..."
cd "$INSTALL_DIR"
dotnet build --configuration Release src/Aura.Api/Aura.Api.csproj

# =============================================================================
# Install Launch Agent
# =============================================================================
header "Installing Launch Agent"

# Create the directory if it doesn't exist
mkdir -p ~/Library/LaunchAgents

# Update plist with correct paths
step "Installing launch agent..."

# Detect dotnet path and root
DOTNET_PATH=$(which dotnet)
DOTNET_ROOT=$(dirname "$DOTNET_PATH")

# Create plist with correct paths
cat > "$PLIST_DST" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.aura.api</string>
    
    <key>ProgramArguments</key>
    <array>
        <string>$DOTNET_PATH</string>
        <string>run</string>
        <string>--project</string>
        <string>$INSTALL_DIR/src/Aura.Api</string>
        <string>--configuration</string>
        <string>Release</string>
        <string>--no-build</string>
    </array>
    
    <key>WorkingDirectory</key>
    <string>$INSTALL_DIR</string>
    
    <key>EnvironmentVariables</key>
    <dict>
        <key>ASPNETCORE_ENVIRONMENT</key>
        <string>Production</string>
        <key>ASPNETCORE_URLS</key>
        <string>http://localhost:5300</string>
        <key>ConnectionStrings__auradb</key>
        <string>Host=localhost;Port=5432;Database=aura;Username=aura;Password=aura</string>
        <key>DOTNET_ROOT</key>
        <string>$DOTNET_ROOT</string>
        <key>PATH</key>
        <string>$DOTNET_ROOT:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin</string>
    </dict>
    
    <key>RunAtLoad</key>
    <true/>
    
    <key>KeepAlive</key>
    <true/>
    
    <key>StandardOutPath</key>
    <string>$LOG_DIR/api.log</string>
    
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/api.error.log</string>
</dict>
</plist>
EOF

# Unload existing service if running
launchctl unload "$PLIST_DST" 2>/dev/null || true

# Load the service
step "Starting Aura service..."
launchctl load "$PLIST_DST"

# =============================================================================
# Verify
# =============================================================================
header "Verifying Installation"

step "Waiting for API to start..."
sleep 5

for i in {1..30}; do
    if curl -s http://localhost:5300/health > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Aura API is running!${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        warn "API not responding yet. Check logs at: $LOG_DIR/api.log"
    fi
    sleep 1
done

# =============================================================================
# Summary
# =============================================================================
header "Installation Complete!"

echo ""
echo -e "Installed components:"
echo -e "${GRAY}  - PostgreSQL 17 with pgvector (native via Homebrew)${NC}"
echo -e "${GRAY}  - Ollama (local LLM)${NC}"
echo -e "${GRAY}  - nomic-embed-text model${NC}"
echo -e "${GRAY}  - qwen2.5-coder:7b model${NC}"
echo -e "${GRAY}  - .NET SDK${NC}"
echo -e "${GRAY}  - Aura API (as launch agent)${NC}"
echo ""
echo -e "Service management:"
echo -e "${CYAN}  Start:   launchctl load ~/Library/LaunchAgents/com.aura.api.plist${NC}"
echo -e "${CYAN}  Stop:    launchctl unload ~/Library/LaunchAgents/com.aura.api.plist${NC}"
echo -e "${CYAN}  Logs:    tail -f $LOG_DIR/api.log${NC}"
echo ""
echo -e "API available at: ${GREEN}http://localhost:5300${NC}"
echo ""
