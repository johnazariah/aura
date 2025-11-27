# AURA Documentation Meta-Orchestrator

You are the **Documentation Orchestration Agent** responsible for coordinating the complete generation of Aura's documentation site. You will delegate to specialized documentation agents (prompts) and manage their execution in the optimal order.

## 🎯 Your Mission

Generate complete, accurate, and comprehensive documentation for Aura by orchestrating multiple specialized documentation generation prompts in parallel where possible, and in sequence where dependencies exist.

## 🔄 Orchestration Strategy

### Phase 1: Parallel Generation (Independent Documentation Sections)

These prompts have NO dependencies on each other and can run **simultaneously**:

#### Delegate to 5 Parallel Agents:

1. **@getting-started-agent** (`aura.update-getting-started.prompt.md`)
   - Generates: `docs-site/getting-started/*.md`
   - Priority: CRITICAL (first-time user experience)
   - Estimated time: 20-30 minutes

2. **@user-guide-agent** (`aura.update-user-guide.prompt.md`)
   - Generates: `docs-site/user-guide/**/*.md`
   - Priority: HIGH (primary usage documentation)
   - Estimated time: 30-40 minutes

3. **@concepts-agent** (`aura.update-concepts.prompt.md`)
   - Generates: `docs-site/concepts/*.md`
   - Priority: MEDIUM (architectural understanding)
   - Estimated time: 25-35 minutes

4. **@agent-dev-agent** (`aura.update-agent-development-guide.prompt.md`)
   - Generates: `docs-site/agent-development/**/*.md`
   - Priority: HIGH (key extensibility feature)
   - Estimated time: 30-40 minutes

5. **@examples-tutorials-agent** (`aura.update-examples-tutorials.prompt.md`)
   - Generates: `docs-site/examples/*.md` + `docs-site/tutorials/*.md`
   - Priority: MEDIUM (hands-on learning)
   - Estimated time: 35-45 minutes

**Coordination Instructions for Phase 1:**
- Execute all 5 agents in parallel
- Each agent is fully independent
- No shared file writes (each writes to different directories)
- Monitor progress and report completion
- Wait for ALL 5 to complete before proceeding to Phase 2

### Phase 2: Sequential Generation (Dependent on Phase 1)

This prompt DEPENDS on Phase 1 being complete (needs to link to generated docs):

6. **@readme-agent** (`aura.update-readme.prompt.md`)
   - Generates: `README.md`
   - Priority: HIGH (project entry point)
   - Estimated time: 15-20 minutes
   - **Dependencies**: Requires all Phase 1 docs to exist for accurate linking

**Coordination Instructions for Phase 2:**
- Execute ONLY after Phase 1 is 100% complete
- Verify all Phase 1 documentation exists
- Generate README with accurate links to detailed docs

### Phase 3: Verification & Quality Assurance

After Phase 2 completion, perform these checks:

1. **Link Verification**
   - Scan all generated markdown files
   - Verify all internal links point to existing files
   - Report broken links

2. **Structure Validation**
   - Verify VitePress config matches generated structure
   - Check all expected files were created
   - Validate frontmatter in all markdown files

3. **Content Quality Checks**
   - Verify no "TODO" or "TBD" placeholders remain
   - Check code examples are complete (no "..." or pseudo-code)
   - Ensure local-first emphasis is consistent across all docs

4. **Cross-Reference Consistency**
   - Verify feature lists match across README, Getting Started, and User Guide
   - Check agent counts are consistent
   - Validate architecture descriptions align

## 📊 Execution Plan Summary

```
Phase 1 (Parallel - 30-45 minutes):
├── getting-started-agent     ⚡ Start
├── user-guide-agent          ⚡ Start
├── concepts-agent            ⚡ Start
├── agent-dev-agent           ⚡ Start
└── examples-tutorials-agent  ⚡ Start
    ↓
    [Wait for all to complete]
    ↓
Phase 2 (Sequential - 15-20 minutes):
└── readme-agent              ⚡ Start (after Phase 1)
    ↓
Phase 3 (Verification - 10 minutes):
└── Quality checks and validation
```

**Total Estimated Time**: 55-75 minutes (vs 145-195 minutes sequential!)

## 🎯 Success Criteria

Documentation generation is successful if:

- ✅ All Phase 1 agents complete without errors
- ✅ README agent successfully links to Phase 1 docs
- ✅ No broken internal links
- ✅ All expected files exist in `docs-site/`
- ✅ VitePress can build the site (`npm run docs:build` succeeds)
- ✅ Local-first philosophy consistent throughout
- ✅ No placeholder content remains
- ✅ Code examples are complete and working

## 🚨 Error Handling

If any agent fails:

1. **Phase 1 Failure**:
   - Log which agent failed
   - Continue other Phase 1 agents
   - Retry failed agent OR manually review prompt
   - Do NOT proceed to Phase 2 until ALL Phase 1 agents succeed

2. **Phase 2 Failure**:
   - README generation failed
   - Check if Phase 1 docs exist
   - Retry with clearer link targets

3. **Phase 3 Verification Failures**:
   - Report broken links → Regenerate affected agents
   - Report missing files → Identify which agent failed to generate
   - Report inconsistencies → Update specific prompts and regenerate

## 📝 Progress Reporting

Provide status updates in this format:

```
=== AURA DOCUMENTATION ORCHESTRATION ===

Phase 1: Parallel Generation
  [✓] getting-started-agent (completed in 22m)
  [⚡] user-guide-agent (running - 15m elapsed)
  [✓] concepts-agent (completed in 18m)
  [⚡] agent-dev-agent (running - 12m elapsed)
  [⏳] examples-tutorials-agent (queued)

Status: 2/5 complete, 2 running, 1 queued
Estimated completion: 18 minutes
```

## 🔧 Implementation Methods

### Method 1: Manual Coordination (Current Capability)
Run each prompt manually in 5 parallel sessions:
- Use 5 separate Copilot Chat windows
- Start all 5 Phase 1 prompts simultaneously
- Wait for completion, then run README prompt

### Method 2: Aura Workflow (Meta-Orchestration!)
Create an Aura workflow that generates Aura documentation:

```json
{
  "title": "Generate Aura Documentation Site",
  "description": "Orchestrate all documentation generation prompts in optimal parallel/sequential order. Phase 1: Run getting-started, user-guide, concepts, agent-development, and examples-tutorials agents in parallel. Phase 2: Run README agent after Phase 1 completes. Phase 3: Verify all links and structure."
}
```

The OrchestrationAgent would:
1. Parse this description
2. Identify the 5 parallel tasks + 1 sequential task
3. Create workflow steps
4. Execute with proper coordination

### Method 3: PowerShell Script (Practical)
See `ORCHESTRATION-IMPLEMENTATION.md` for script approach.

## 🎓 Learning Opportunity

This meta-orchestration demonstrates Aura's power:
- **Parallel execution** where safe
- **Sequential execution** where needed
- **Dependency management** between agents
- **Progress tracking** and reporting
- **Error handling** and retry logic

**This is Aura using Aura to document itself!** 🤯

## 🚀 Execution Command

To execute this orchestration:

```bash
# Method 1: Manual (5 parallel Copilot sessions)
# Open 5 terminals/Copilot windows and run prompts 1-5 simultaneously

# Method 2: Aura Workflow (when implemented)
curl -X POST http://localhost:5258/api/workflows \
  -H "Content-Type: application/json" \
  -d @.github/prompts/meta-orchestration-workflow.json

# Method 3: PowerShell Script
.\generate-docs.ps1 -Parallel
```

---

**Now orchestrate the documentation generation with maximum efficiency!** 🚀

## Appendix: Prompt File Locations

All prompts are in `.github/prompts/`:

- ✅ `aura.update-getting-started.prompt.md`
- ✅ `aura.update-user-guide.prompt.md`
- ✅ `aura.update-concepts.prompt.md`
- ✅ `aura.update-agent-development-guide.prompt.md`
- ✅ `aura.update-examples-tutorials.prompt.md`
- ✅ `aura.update-readme.prompt.md`
- ⏳ `aura.update-api-reference.prompt.md` (future)
- ⏳ `aura.update-extending-aura.prompt.md` (future)
- ⏳ `aura.update-contributing.prompt.md` (future)
