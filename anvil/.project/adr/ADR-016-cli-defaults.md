# ADR-016: CLI Defaults and Output Behavior

## Status
Accepted

## Date
2026-01-30

## Context

Anvil needs sensible defaults for:
1. How to connect to Aura
2. Where to find scenarios
3. Where to write reports
4. How much output to show during execution

These decisions affect user experience and automation scenarios.

## Decisions

### 1. Aura Base URL

**Precedence** (highest to lowest):
1. `--url` command-line flag
2. `AURA_URL` environment variable
3. `http://localhost:5300` hardcoded fallback

**Rationale**: Flags for one-off overrides, env var for persistent config, sensible default for zero-config local use.

### 2. Scenario Discovery

**Default**: Auto-discover from `./scenarios/` relative to current working directory.

**Override**: Pass explicit path as argument: `anvil run ./custom/path`

**Rationale**: Matches conventions from test frameworks (jest, pytest). CWD-relative allows running from any directory.

### 3. Report Output

**Default**: `./reports/anvil-{ISO8601-timestamp}.json`
- Directory created if missing
- Timestamp format: `2026-01-30T14-30-00` (filesystem-safe)

**Override**: `--output ./custom/report.json`

**Rationale**: Predictable location, timestamped to avoid overwrites, easy to find latest.

### 4. Console Output Verbosity

**Default**: Verbose (show everything)
- Step-by-step progress
- Agent output
- Expectation validation
- Timing information

**Quiet** (`-q` or `--quiet`): Summary only
- Pass/fail per scenario
- Final counts
- Errors only

**Silent** (`-qq` or `--silent`): Exit code only
- No console output
- For automation/scripting

**Rationale**: Verbose default because silent AI operations cause anxiety. Users expect to see activity. Quiet modes for automation.

### 5. Log File Verbosity

**Always verbose**, regardless of console settings.
- Written to `./logs/anvil-{timestamp}.log`
- Contains timestamps, API calls, full responses, stack traces
- Enables post-mortem debugging even with `-qq` console

**Rationale**: Console output is ephemeral; logs persist. Never lose debug information just because user wanted quiet console.

## Consequences

### Positive
- Zero-config works out of the box for common case
- Flexible overrides for non-standard setups
- Verbose default reduces "is it working?" anxiety
- Logs always available for debugging

### Negative
- Verbose default may be noisy for experienced users (use `-q`)
- Multiple output directories (`reports/`, `logs/`) to manage

## CLI Summary

```bash
# All defaults
anvil run

# Custom Aura URL
anvil run --url http://192.168.1.5:5300

# Custom scenario path
anvil run ./my-scenarios

# Custom report output
anvil run --output ./results.json

# Quiet mode (summary only)
anvil run -q

# Silent mode (for scripts)
anvil run -qq
```
