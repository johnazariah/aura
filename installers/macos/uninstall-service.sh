#!/bin/bash
# Uninstall Aura macOS service
# Usage: ./installers/macos/uninstall-service.sh

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[0;33m'
NC='\033[0m'

header() { echo -e "\n${CYAN}============================================================${NC}"; echo -e "${CYAN} $1${NC}"; echo -e "${CYAN}============================================================${NC}"; }
step() { echo -e "${GREEN}>> $1${NC}"; }
warn() { echo -e "${YELLOW}!! $1${NC}"; }

INSTALL_DIR="/usr/local/share/aura"
LOG_DIR="/usr/local/var/log/aura"
PLIST_DST="$HOME/Library/LaunchAgents/com.aura.api.plist"

header "Uninstalling Aura Service"

# Stop and unload the service
if [ -f "$PLIST_DST" ]; then
    step "Stopping Aura service..."
    launchctl unload "$PLIST_DST" 2>/dev/null || true
    rm -f "$PLIST_DST"
    step "Launch agent removed"
else
    warn "Launch agent not found"
fi

# Ask about removing files
echo ""
read -p "Remove Aura files from $INSTALL_DIR? (y/N) " remove_files
if [[ "$remove_files" =~ ^[Yy]$ ]]; then
    step "Removing Aura files..."
    sudo rm -rf "$INSTALL_DIR"
    step "Aura files removed"
fi

# Ask about removing logs
read -p "Remove log files from $LOG_DIR? (y/N) " remove_logs
if [[ "$remove_logs" =~ ^[Yy]$ ]]; then
    step "Removing log files..."
    sudo rm -rf "$LOG_DIR"
    step "Log files removed"
fi

# Ask about removing database
read -p "Remove Aura database? (y/N) " remove_db
if [[ "$remove_db" =~ ^[Yy]$ ]]; then
    step "Removing database..."
    export PATH="/opt/homebrew/opt/postgresql@17/bin:$PATH"
    dropdb aura 2>/dev/null || warn "Database 'aura' not found"
    psql postgres -c "DROP ROLE IF EXISTS aura;" 2>/dev/null || true
    step "Database removed"
fi

header "Uninstall Complete"

echo ""
echo -e "The following were NOT removed (manual cleanup if desired):"
echo -e "${YELLOW}  - PostgreSQL 17: brew uninstall postgresql@17${NC}"
echo -e "${YELLOW}  - pgvector: brew uninstall pgvector${NC}"
echo -e "${YELLOW}  - Ollama: brew uninstall --cask ollama${NC}"
echo -e "${YELLOW}  - .NET SDK: brew uninstall --cask dotnet-sdk${NC}"
echo ""
