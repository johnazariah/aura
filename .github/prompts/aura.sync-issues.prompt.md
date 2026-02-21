---
description: Sync open GitHub issues into Aura stories — find issues without stories and import them.
---

# Sync GitHub Issues

Pull open issues from a GitHub repository and create Aura stories for ones that don't have linked stories yet.

## Steps

1. **Determine the repository.** Check the current git remote:
   ```
   cd <workspace> && git remote get-url origin
   ```
   Parse the owner and repo from the URL.

2. **List open GitHub issues.** Use GitHub tools to fetch issues:
   ```
   gh issue list --state open --json number,title,labels,url --limit 20
   ```
   Or use GitHub MCP tools if available.

3. **Check for existing stories.** Call `aura_workflow(operation: "list")` and match by `issueUrl` or `issueNumber`.

4. **Show unlinked issues.** Present a table of issues that don't have Aura stories:

   | # | Issue | Title | Labels |
   |---|-------|-------|--------|
   | 1 | #42 | Add retry logic | bug, enhancement |
   | 2 | #45 | Update README | docs |

5. **Ask which to import.** Let the user pick which issues to turn into stories (all, specific numbers, or by label filter).

6. **Create stories.** For each selected issue:
   ```
   aura_workflow(operation: "create", issueUrl: "<issueUrl>", repositoryPath: "<repoPath>")
   ```

7. **Report results.** Show created stories with their worktree paths and branches.

## Options

The user may specify filters:
- `--labels bug,enhancement` — Only sync issues with specific labels
- `--repo owner/repo` — Sync from a different repo than the current one
- `--limit N` — Maximum issues to show

## Notes

- Only syncs open issues (closed ones are not imported)
- Issues that already have linked stories are skipped
- Each imported issue gets its own worktree and branch
- A "work started" comment is posted to the GitHub issue
