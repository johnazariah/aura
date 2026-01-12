# Create Logical Commits from Staged Changes

Analyze the currently staged changes and group them into multiple logical commits.

## Instructions

1. **Analyze staged changes** using `get_changed_files`

2. **Run formatting first** to avoid formatting noise in commits:
   ```powershell
   dotnet format Aura.sln --verbosity quiet
   ```

3. **Identify logical groups** based on:
   - **Feature area**: Same feature or component (e.g., "workflow improvements", "git integration")
   - **Change type**: Refactoring, bug fixes, documentation, new features
   - **Conventional commit scope**: What prefix/scope makes sense (feat, fix, refactor, docs, style, test, chore)

4. **Common groupings to look for**:
   - Documentation updates (docs/)
   - Code style/formatting changes (primary constructors, whitespace)
   - New feature implementation
   - Bug fixes
   - Test additions
   - Configuration changes
   - Dependency updates

5. **For each logical group**, execute:
   ```powershell
   # First unstage everything
   git reset HEAD
   
   # Stage files for first commit
   git add <file1> <file2> ...
   git commit -m "<type>(<scope>): <description>"
   
   # Stage files for second commit (skip hooks after first commit passed)
   git add <file3> <file4> ...
   git commit --no-verify -m "<type>(<scope>): <description>"
   
   # Repeat for remaining groups
   ```

6. **Commit message format** (conventional commits):
   - `feat(scope):` - New feature
   - `fix(scope):` - Bug fix
   - `refactor(scope):` - Code refactoring without behavior change
   - `docs(scope):` - Documentation only
   - `style(scope):` - Formatting, whitespace, etc.
   - `test(scope):` - Adding or updating tests
   - `chore(scope):` - Maintenance, dependencies, build

7. **Order commits logically**:
   - Infrastructure/refactoring first
   - Features second
   - Documentation last

## Example Output

For a mix of changes, you might create:
1. `refactor(foundation): convert classes to primary constructors`
2. `feat(workflow): add git squash on workflow completion`
3. `fix(tools): case-insensitive WorkingDirectory injection`
4. `docs: fix markdown table formatting and code block hints`

## Constraints

- Keep related changes together (don't split a feature across commits)
- Each commit should be atomic and buildable
- Use clear, descriptive commit messages
- If unsure, ask the user which grouping they prefer
