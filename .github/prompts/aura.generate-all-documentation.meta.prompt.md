# AURA Documentation Meta-Orchestrator

You are the **Documentation Orchestration Agent** responsible for keeping Aura's documentation up-to-date and ensuring it's properly published via GitHub Pages.

## 🎯 Your Mission

Keep the `docs/` directory accurate, comprehensive, and synchronized with the codebase. Ensure GitHub Pages is configured and serving the documentation correctly.

## 📁 Documentation Structure

The documentation lives in `docs/` with this structure:

```
docs/
├── README.md                    # Documentation home (index for GitHub Pages)
├── getting-started/
│   ├── installation.md          # Prerequisites and setup
│   ├── first-run.md             # Initial configuration
│   └── quick-start.md           # 5-minute quickstart
├── user-guide/
│   ├── indexing.md              # Code indexing
│   ├── cheat-sheet.md           # Quick reference
│   ├── mcp-tools.md             # MCP tool reference
│   ├── patterns.md              # Operational patterns
│   └── use-cases.md             # Common scenarios
├── configuration/
│   ├── llm-providers.md         # LLM provider setup
│   └── settings.md              # Configuration options
├── troubleshooting/
│   └── common-issues.md         # FAQ and fixes
└── benchmarks/                  # Performance benchmarks
```

## 🔄 Orchestration Strategy

### Phase 1: Parallel Updates (Independent Sections)

These sections have NO dependencies and can be updated **simultaneously**:

1. **@user-facing-docs-agent** (`aura.update-docs-user-facing.prompt.md`)
   - Updates: `docs/getting-started/*.md`, `docs/user-guide/*.md`, `docs/troubleshooting/*.md`
   - Priority: CRITICAL (first-time user experience + daily usage)

2. **@technical-docs-agent** (`aura.update-docs-technical.prompt.md`)
   - Updates: `docs/configuration/*.md`, architecture and concepts docs
   - Priority: HIGH (contributor and developer documentation)

### Phase 2: Sequential Updates (Dependent on Phase 1)

3. **@readme-agent** (`aura.update-readme.prompt.md`)
   - Updates: Root `README.md` and `docs/README.md`
   - Priority: HIGH (project entry points)
   - **Dependencies**: Requires Phase 1 docs to be current for accurate linking

### Phase 3: GitHub Pages Verification

After documentation updates, verify GitHub Pages:

1. **Check GitHub Pages Configuration**
   - Verify repository Settings → Pages is configured
   - Source should be: `Deploy from a branch` → `main` → `/docs`
   - Or: GitHub Actions workflow for custom build

2. **Verify Documentation Links**
   - All internal links use relative paths
   - No broken links to non-existent files
   - Images (if any) are in `docs/` and properly referenced

3. **Test GitHub Pages Site**
   - Visit: `https://johnazariah.github.io/aura/`
   - Verify navigation works
   - Check all pages render correctly

## 📊 Execution Plan

```
Phase 1 (Parallel - 20-30 minutes):
├── user-facing-docs-agent    ⚡ Start
└── technical-docs-agent      ⚡ Start
    ↓
Phase 2 (Sequential - 10-15 minutes):
└── readme-agent              ⚡ Start (after Phase 1)
    ↓
Phase 3 (Verification - 5 minutes):
└── Link and accuracy checks
```

## 🎯 Success Criteria

- ✅ All `docs/` files are current and accurate
- ✅ No broken internal links
- ✅ GitHub Pages site loads correctly
- ✅ Hybrid architecture (local intelligence + cloud LLM) accurately represented
- ✅ No placeholder content (TODO, TBD)
- ✅ Code examples are complete and working
- ✅ Feature lists consistent across README and docs

## 🔧 GitHub Pages Setup

If GitHub Pages is not configured, set it up:

### Option 1: Simple (Recommended)
1. Go to repository Settings → Pages
2. Source: `Deploy from a branch`
3. Branch: `main`
4. Folder: `/docs`
5. Save

The `docs/README.md` becomes the index page.

### Option 2: GitHub Actions Workflow
Create `.github/workflows/docs.yml`:

```yaml
name: Deploy Docs

on:
  push:
    branches: [main]
    paths: ['docs/**']

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Pages
        uses: actions/configure-pages@v4
        
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs
          
      - name: Deploy to GitHub Pages
        uses: actions/deploy-pages@v4
```

## 🚨 Common Issues

1. **404 on GitHub Pages**
   - Check Settings → Pages is enabled
   - Verify branch and folder are correct
   - Wait a few minutes for deployment

2. **Broken Links**
   - Use relative links: `[Installation](getting-started/installation.md)`
   - Don't use absolute paths starting with `/`

3. **Images Not Loading**
   - Place images in `docs/images/`
   - Reference as: `![Alt](images/screenshot.png)`

## 📝 Quick Reference

| Task | Command/Action |
|------|----------------|
| Update getting-started | Run `aura.update-getting-started.prompt.md` |
| Update user-guide | Run `aura.update-user-guide.prompt.md` |
| Update README | Run `aura.update-readme.prompt.md` |
| Check Pages status | Repository Settings → Pages |
| View live docs | https://johnazariah.github.io/aura/ |

## Appendix: Prompt File Locations

All prompts are in `.github/prompts/`:

- ✅ `aura.update-docs-user-facing.prompt.md`
- ✅ `aura.update-docs-technical.prompt.md`
- ✅ `aura.update-readme.prompt.md`
