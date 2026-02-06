#!/usr/bin/env bash
#
# Bootstrap a new project with the SDD framework.
#
# Usage:
#   .sdd/bootstrap.sh <project-name> [--skip-git-config] [--force]
#
# Run this script from your project root after copying .sdd/ into it.
#

set -e

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
GRAY='\033[0;90m'
BLUE='\033[0;34m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

step() { echo -e "${CYAN}→ $1${NC}"; }
done_msg() { echo -e "${GREEN}✓ $1${NC}"; }
skip_msg() { echo -e "${YELLOW}○ $1 (already exists)${NC}"; }
info() { echo -e "${GRAY}  $1${NC}"; }

# Parse arguments
PROJECT_NAME=""
SKIP_GIT_CONFIG=false
FORCE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-git-config)
            SKIP_GIT_CONFIG=true
            shift
            ;;
        --force)
            FORCE=true
            shift
            ;;
        *)
            if [[ -z "$PROJECT_NAME" ]]; then
                PROJECT_NAME="$1"
            fi
            shift
            ;;
    esac
done

if [[ -z "$PROJECT_NAME" ]]; then
    echo "Usage: .sdd/bootstrap.sh <project-name> [--skip-git-config] [--force]"
    exit 1
fi

echo ""
echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║           SDD Framework Bootstrap                        ║${NC}"
echo -e "${BLUE}║           Spec-Driven Development                        ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Verify we're in the right place
if [[ ! -d ".sdd/templates" ]]; then
    echo "Error: Cannot find .sdd/templates/. Run this script from your project root after copying .sdd/ into it."
    exit 1
fi

# Create .project directory structure
step "Creating .project/ directory structure..."

directories=(
    ".project/adr"
    ".project/architecture"
    ".project/coding-guidelines"
    ".project/backlog"
    ".project/research"
    ".project/plans"
    ".project/changes"
    ".project/reviews"
    ".project/handoffs"
    ".project/completed/backlog"
    ".project/completed/research"
    ".project/completed/plans"
    ".project/completed/changes"
    ".project/completed/reviews"
)

for dir in "${directories[@]}"; do
    if [[ ! -d "$dir" ]]; then
        mkdir -p "$dir"
        info "Created $dir"
    fi
done
done_msg "Directory structure created"

# Helper to copy templates with substitution
copy_template() {
    local source="$1"
    local dest="$2"
    local substitute="${3:-false}"

    if [[ -f "$dest" && "$FORCE" != "true" ]]; then
        skip_msg "$dest"
        return
    fi

    if [[ "$substitute" == "true" ]]; then
        local today=$(date +%Y-%m-%d)
        local project_lower=$(echo "$PROJECT_NAME" | tr '[:upper:]' '[:lower:]')
        sed -e "s/\[Project Name\]/$PROJECT_NAME/g" \
            -e "s/\[project-name\]/$project_lower/g" \
            -e "s/YYYY-MM-DD/$today/g" \
            "$source" > "$dest"
    else
        cp "$source" "$dest"
    fi
    done_msg "Created $dest"
}

# Copy templates with project name substitution
step "Setting up project files from templates..."

copy_template ".sdd/templates/VISION.md" ".project/VISION.md" true
copy_template ".sdd/templates/STATUS.md" ".project/STATUS.md" true
copy_template ".sdd/templates/AGENTS.md" "AGENTS.md" true

# Create .project/README.md
if [[ ! -f ".project/README.md" || "$FORCE" == "true" ]]; then
    cat > ".project/README.md" << EOF
# $PROJECT_NAME Project Documentation

This directory contains all project-specific documentation and work tracking.

## Quick Links

| Document | Purpose |
|----------|---------|
| [VISION.md](VISION.md) | Product vision and success criteria |
| [STATUS.md](STATUS.md) | Current project state |
| [architecture/](architecture/) | System architecture |
| [adr/](adr/) | Architecture Decision Records |
| [coding-guidelines/](coding-guidelines/) | Language-specific standards |

## Work Tracking

| Folder | Contains |
|--------|----------|
| [backlog/](backlog/) | Work items to be done |
| [research/](research/) | Research artifacts |
| [plans/](plans/) | Implementation plans |
| [changes/](changes/) | Change documentation |
| [reviews/](reviews/) | Review results |
| [handoffs/](handoffs/) | Session handoff notes |
| [completed/](completed/) | Archived completed work |

## For AI Assistants

Read these in order:
1. \`VISION.md\` - What we're building
2. \`STATUS.md\` - Where we are
3. \`architecture/\` - How it works
4. Relevant ADRs for your task
EOF
    done_msg "Created .project/README.md"
else
    skip_msg ".project/README.md"
fi

# Git commit template
if [[ "$SKIP_GIT_CONFIG" != "true" ]]; then
    step "Configuring git commit template..."
    if [[ -d ".git" ]]; then
        git config commit.template .sdd/templates/commit-message.txt
        done_msg "Git commit template configured"
    else
        info "No .git directory found, skipping git config"
    fi
fi

# Copy .github/agents if they exist in reference and not in project
if [[ -d ".sdd/../.github/agents" && ! -d ".github/agents" ]]; then
    step "Copying agent definitions..."
    mkdir -p ".github/agents"
    cp .sdd/../.github/agents/* ".github/agents/"
    done_msg "Agent definitions copied to .github/agents/"
fi

echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║           Bootstrap Complete! 🎉                         ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${WHITE}Next steps:${NC}"
echo -e "${GRAY}  1. Edit .project/VISION.md with your project vision${NC}"
echo -e "${GRAY}  2. Run @backlog-builder to create initial backlog items${NC}"
echo -e "${GRAY}  3. Use @next-backlog-item to pick your first task${NC}"
echo ""
echo -e "${GRAY}See .sdd/README.md for the full workflow.${NC}"
echo ""
