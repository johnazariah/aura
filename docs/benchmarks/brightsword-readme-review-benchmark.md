# BrightSword README Review Benchmark

This document defines what a **high-quality** LLM-generated review of the BrightSword repository should contain. Use this as a benchmark to evaluate agent output quality.

## Ground Truth: BrightSword Repository Facts

### Repository Structure

- **4 main packages**: SwissKnife, Crucible, Feber, Squid
- **4 test projects**: corresponding `*.Tests` projects
- **2 sample apps**: `BrightSword.Feber.SamplesApp`, `BrightSword.Squid.Samples`
- **88 C# source files** (excluding bin/obj)
- **Target framework**: .NET 10

### Package Purposes (Must Mention)

1. **BrightSword.SwissKnife** - Utility classes and extension methods (base package, no dependencies)
2. **BrightSword.Crucible** - MSTest testing utilities with `ExpectException<T>()` fluent API
3. **BrightSword.Feber** - Automated delegate generation using LINQ Expression trees (depends on SwissKnife)
4. **BrightSword.Squid** - Runtime type emission using Reflection.Emit (depends on Feber and SwissKnife)

### Dependency Chain (Critical Knowledge)

```text
SwissKnife (base) ← Feber ← Squid
Crucible (independent, depends only on MSTest)
```

### Key Build Commands

```bash
# Canonical CI build
dotnet msbuild Build.proj /t:CI /p:Configuration=Release /v:minimal

# Restore, build, test
dotnet msbuild Build.proj /t:"Restore;BuildPackages;BuildTests;Test" /p:Configuration=Release

# Run sample
dotnet run --project BrightSword.Feber.SamplesApp/BrightSword.Feber.SamplesApp.csproj
```

### Documentation Files That Exist

- `docs/ARCHITECTURE.md` - Complete architecture overview
- `docs/BUILD.md` - Build instructions
- `docs/CONTRIBUTING.md` - Contribution guidelines  
- `docs/VERSIONING.md` - Version management strategy
- `docs/CICD.md` - CI/CD pipeline documentation
- Per-package docs in `{Package}/docs/` folders

### Key Technical Details

- **Feber performance**: First call ~10-100ms (reflection + expression building + JIT), subsequent calls <0.001ms (cached compiled delegates)
- **Build orchestration**: Uses `Build.proj` for MSBuild targets
- **Versioning**: Per-package `version.props` files
- **License**: Creative Commons BY 4.0

---

## Quality Scoring Rubric

### Essential Elements (Must Include) - 50 points

| Element | Points | Description |
|---------|--------|-------------|
| Correct package names | 10 | All 4 packages mentioned by correct name |
| Package purposes accurate | 10 | Each package's purpose correctly described |
| Dependency chain correct | 10 | SwissKnife → Feber → Squid chain mentioned |
| Build commands present | 10 | At least one valid build command |
| Documentation refs valid | 10 | Links to real doc files that exist |

### Good-to-Have Elements - 30 points

| Element | Points | Description |
|---------|--------|-------------|
| Performance characteristics | 5 | Mentions Feber's compilation/caching pattern |
| Target framework | 5 | Mentions .NET 10 |
| Test structure mentioned | 5 | References test projects |
| Sample apps mentioned | 5 | References sample applications |
| CI/CD overview | 5 | Mentions Build.proj or GitHub Actions |
| Architecture overview | 5 | Mentions monorepo structure |

### Bonus Elements - 20 points

| Element | Points | Description |
|---------|--------|-------------|
| Expression trees mentioned | 5 | Technical accuracy on Feber |
| Reflection.Emit mentioned | 5 | Technical accuracy on Squid |
| Warm-up patterns | 5 | Mentions Lazy<T> or warm-up utilities |
| Contribution workflow | 5 | Mentions PR, branching, or guidelines |

---

## Example: Hallucinated vs Grounded Output

### ❌ Hallucinated (Bad) Example

```text
BrightSword is a .NET project for building applications. It includes various 
utilities and helpers. To install, run `npm install brightsword`. The main 
class is `BrightSwordCore` which you can use like:

var sword = new BrightSwordCore();
sword.Execute();
```

**Problems:**

- Invented non-existent `npm install` command
- Made up `BrightSwordCore` class that doesn't exist
- No mention of actual packages
- Generic description not based on repo

### ✅ Grounded (Good) Example

```
BrightSword is a .NET monorepo containing four packages:

1. **BrightSword.SwissKnife** - Utility classes and extension methods
2. **BrightSword.Crucible** - MSTest testing utilities with ExpectException<T>
3. **BrightSword.Feber** - Expression-based delegate generation (depends on SwissKnife)
4. **BrightSword.Squid** - Runtime type emission with Reflection.Emit

To build: `dotnet msbuild Build.proj /t:CI /p:Configuration=Release`
To run sample: `dotnet run --project BrightSword.Feber.SamplesApp/BrightSword.Feber.SamplesApp.csproj`

See docs/ARCHITECTURE.md for full system design.
```

**Why it's good:**

- Correct package names
- Accurate descriptions matching repo docs
- Valid commands from actual README
- References real documentation files
- Mentions dependency chain

---

## Scoring the Latest Output

### Azure OpenAI Output (After RAG Improvements)

**Essential Elements (43/50):**

- ✅ Correct package names (10) - Mentions Feber, Crucible, SwissKnife
- ⚠️ Package purposes (7/10) - Partially accurate, missing Squid details
- ❌ Dependency chain (3/10) - Not explicitly stated
- ✅ Build commands (10) - Multiple valid commands
- ✅ Documentation refs (10) - Real paths like `docs/BUILD.md`

**Good-to-Have (20/30):**

- ❌ Performance characteristics (0)
- ❌ Target framework (0)
- ⚠️ Test structure (3/5) - Mentioned briefly
- ⚠️ Sample apps (3/5) - Shows run command
- ⚠️ CI/CD overview (3/5) - Mentioned but not detailed
- ✅ Architecture overview (5) - Good structure

**Bonus (0/20):**

- ❌ Expression trees (0)
- ❌ Reflection.Emit (0)
- ❌ Warm-up patterns (0)
- ⚠️ Contribution workflow (3/5)

**Total: 66/100** - Good but missing technical depth

---

## Improvement Suggestions

To improve agent output quality:

1. **Add more documentation-specific RAG queries**: Include queries for "expression tree", "Reflection.Emit", "performance cache"
2. **Increase context window**: Allow more RAG chunks for documentation tasks
3. **Include key file content**: Ensure ARCHITECTURE.md and package READMEs are in the index
4. **Prompt engineering**: Ask agent to include technical details and dependencies
