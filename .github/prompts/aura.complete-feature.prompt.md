---
description: Ceremony for completing a feature and updating all project documentation
---
# Complete Feature Ceremony

You are completing work on a feature. Follow these steps EXACTLY to ensure all documentation is updated consistently.

## Feature Being Completed
Name: {{featureName}}
File: {{featureFile}}

## Required Steps

### Step 1: Move Feature File to Completed

If the feature file is in `features/upcoming/`:
1. Move it to `features/completed/` with the same kebab-case filename
2. Keep the filename descriptive (no numbers)

### Step 2: Update Feature Header

The feature file MUST have this exact header format:

```markdown
# Feature Title

**Status:** ✅ Complete  
**Completed:** {{currentDate}}  
**Last Updated:** {{currentDate}}

## Overview
...
```

Ensure:
- Status shows `✅ Complete`
- Completed date is today's date (YYYY-MM-DD format)
- Last Updated is today's date

### Step 3: Update features/README.md Index

Add the feature to the "Completed Features" table in `.project/features/README.md`:

```markdown
| [Feature Name](completed/feature-name.md) | Brief description | {{currentDate}} |
```

The table has three columns: Feature, Description, Completed date.
Insert in chronological order (newest at bottom).

### Step 4: Verify Consistency

Check that:
- [ ] Feature file is in `features/completed/`
- [ ] Feature file has `**Status:** ✅ Complete`
- [ ] Feature file has `**Completed:** YYYY-MM-DD`
- [ ] README.md index includes the feature with correct link and date
- [ ] No broken links (old numbered filenames)

### Step 5: Batch Commit All Related Changes

Before committing the documentation, commit all code changes in logical batches:

1. **Run `git status --short`** to see all modified/new files
2. **Group related files by feature/area** using this priority:
   - Core functionality changes (single feature = single commit)
   - API/endpoint changes
   - Database/schema changes  
   - Prompts/agent changes
   - Extension/UI changes
   - Tests
   - Documentation and samples

3. **For each group, create an atomic commit:**
   ```powershell
   git add <related-files>
   git commit -m "<type>(<scope>): <description>"
   ```

4. **Use conventional commit types:**
   - `feat` - New feature
   - `fix` - Bug fix
   - `refactor` - Code restructuring (no behavior change)
   - `chore` - Maintenance, config, dependencies
   - `docs` - Documentation only
   - `test` - Tests only

5. **Common scopes for Aura:**
   - `git`, `rag`, `workflow`, `api`, `extension`, `prompts`, `db`, `foundation`, `developer`

6. **Finally, commit the feature documentation:**
   ```
   docs(features): complete {feature-name}

   - Move to features/completed/
   - Update status and completion date
   - Update README index
   ```

### Step 6: Push All Commits

After all commits are made:
```powershell
git push
```

## Validation

Before finishing, confirm:
1. The feature file exists at `.project/features/completed/{feature-name}.md`
2. The file contains `**Completed:** {{currentDate}}`
3. The README index at `.project/features/README.md` lists the feature

## Example

For completing "smart-content":

1. Move `.project/features/upcoming/smart-content.md` → `.project/features/completed/smart-content.md`
2. Update header:
   ```markdown
   # Smart Content
   
   **Status:** ✅ Complete  
   **Completed:** 2025-12-12  
   **Last Updated:** 2025-12-12
   ```
3. Add to README:
   ```markdown
   | [Smart Content](completed/smart-content.md) | LLM-driven content extraction | 2025-12-12 |
   ```
4. Commit: `docs(features): complete smart-content`
