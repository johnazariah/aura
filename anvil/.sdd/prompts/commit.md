# Create Logical Commits from Staged Changes

Analyze the currently staged changes and group them into multiple logical commits.

## Instructions

1. **Analyze staged changes** using `get_changed_files`

2. **Run formatting first** (if applicable) to avoid formatting noise in commits:
   - **Python**: `ruff format . && ruff check --fix .`
   - **Go**: `gofmt -w .`
   - **.NET**: `dotnet format --verbosity quiet`
   - **Node.js**: `npm run format` or `npx prettier --write .`
   - Skip if no formatter configured or if changes are documentation-only

3. **Identify logical groups** based on:
   - **Feature area**: Same feature or component
   - **Change type**: Refactoring, bug fixes, documentation, new features
   - **Conventional commit scope**: What prefix/scope makes sense

4. **Common groupings to look for**:
   - Documentation updates (*.md, docs/)
   - Code style/formatting changes
   - New feature implementation
   - Bug fixes
   - Test additions
   - Configuration changes (.gitignore, pyproject.toml, package.json)
   - Dependency updates
   - Infrastructure/CI changes (.github/, Makefile)

5. **For each logical group**, execute:
   ```powershell
   # First unstage everything
   git reset HEAD
   
   # Stage files for first commit
   git add <file1> <file2> ...
   git commit -m "<type>(<scope>): <description>"
   
   # Stage files for second commit (skip hooks - see note below)
   git add <file3> <file4> ...
   git commit --no-verify -m "<type>(<scope>): <description>"
   
   # Repeat for remaining groups with --no-verify
   ```

   > **⚡ Pre-commit Hook Efficiency:** Run the full pre-commit hook only on the 
   > **first commit** in a batch. Use `--no-verify` for all subsequent commits.
   > The first commit validates formatting, secrets, and linting for all files—
   > re-running these checks on every commit in a batch wastes time without benefit.

6. **Commit message format** (conventional commits):
   | Type | Description |
   |------|-------------|
   | `feat(scope):` | New feature |
   | `fix(scope):` | Bug fix |
   | `refactor(scope):` | Code refactoring without behavior change |
   | `docs(scope):` | Documentation only |
   | `style(scope):` | Formatting, whitespace, etc. |
   | `test(scope):` | Adding or updating tests |
   | `chore(scope):` | Maintenance, dependencies, build |
   | `ci(scope):` | CI/CD changes |

7. **Order commits logically**:
   - Infrastructure/refactoring first
   - Features second
   - Tests third
   - Documentation last

## Example Output

For a mix of changes, you might create:
1. `refactor(services): extract interface for HTTP adapter`
2. `feat(cli): add get-data command`
3. `test(services): add unit tests for data service`
4. `docs: update README with new command usage`

## Constraints

- Keep related changes together (don't split a feature across commits)
- Each commit should be atomic and buildable
- Use clear, descriptive commit messages
- Scope is optional for broad changes (e.g., `docs:` instead of `docs(readme):`)
- If unsure about grouping, ask the user which grouping they prefer

## Co-Authorship Attribution

Add AI co-authorship to commits to acknowledge the coding agent's contribution.

1. **Identify yourself** by checking your model/agent metadata:
   - Model name (e.g., Claude Opus 4.5, GPT-4, etc.)
   - Platform (e.g., GitHub Copilot, Cursor, Cline, etc.)

2. **Add Co-authored-by trailer** to commit messages:
   ```
   git commit -m "<type>(<scope>): <description>
   
   Co-authored-by: <agent-name> <agent-email>"
   ```

3. **Common agent attributions**:
   | Agent | Co-authored-by |
   |-------|----------------|
   | GitHub Copilot (default) | `Co-authored-by: Copilot <copilot@github.com>` |
   | Claude via Copilot | `Co-authored-by: Claude (via Copilot) <claude@anthropic.com>` |
   | Claude via Cursor | `Co-authored-by: Claude (via Cursor) <claude@anthropic.com>` |
   | GPT-4 via Copilot | `Co-authored-by: GPT-4 (via Copilot) <noreply@openai.com>` |

4. **Example commit with attribution**:
   ```powershell
   git commit -m "feat(cli): add get-data command

   Co-authored-by: Claude (via Copilot) <claude@anthropic.com>"
   ```

5. **For multi-line commit messages in PowerShell**:
   ```powershell
   git commit -m "feat(cli): add get-data command`n`nCo-authored-by: Claude (via Copilot) <claude@anthropic.com>"
   ```
   
   Or use a commit message file:
   ```powershell
   @"
   feat(cli): add get-data command
   
   Co-authored-by: Claude (via Copilot) <claude@anthropic.com>
   "@ | Set-Content .git/COMMIT_MSG
   git commit -F .git/COMMIT_MSG
   Remove-Item .git/COMMIT_MSG
   ```
