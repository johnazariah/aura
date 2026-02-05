# Anvil Test Scenarios

This directory contains test scenarios for validating Aura's code generation capabilities.

## Directory Structure

```
scenarios/
├── csharp/           # C# scenarios
│   └── hello-world.yaml
├── python/           # Python scenarios (future)
├── typescript/       # TypeScript scenarios (future)
└── README.md         # This file
```

## Scenario Format

Each scenario is a YAML file with the following structure:

```yaml
# Required fields
name: scenario-name           # Unique identifier (kebab-case)
description: Short description of what this scenario tests
language: csharp              # Programming language (csharp, python, typescript, etc.)
repository: /path/to/repo     # Repository where story will be executed

story:
  title: Story title          # Title shown in Aura UI
  description: |              # Detailed description for the AI
    Multi-line description
    of the development task

expectations:                 # What to verify after story completes
  - type: compiles
    description: Code should compile

# Optional fields
timeoutSeconds: 300           # Max seconds to wait (default: 300)
tags:                         # For filtering scenarios
  - beginner
  - validation
```

## Expectation Types

### `compiles`
Verifies the story completed without build errors.

```yaml
- type: compiles
  description: Code should compile without errors
```

### `tests_pass`
Verifies all tests passed during story execution.

```yaml
- type: tests_pass
  description: All unit tests should pass
```

### `file_exists`
Verifies a specific file exists in the worktree.

```yaml
- type: file_exists
  description: Controller should be created
  path: src/Controllers/UserController.cs
```

### `file_contains`
Verifies a file contains text matching a regex pattern.

```yaml
- type: file_contains
  description: Should have validation attribute
  path: src/Models/User.cs
  pattern: '\[Required\]'
```

## Running Scenarios

### Validate scenarios (no execution)
```bash
anvil validate scenarios/
```

### Run all scenarios
```bash
anvil run scenarios/
```

### Run a specific scenario
```bash
anvil run scenarios/csharp/hello-world.yaml
```

### Run with custom Aura URL
```bash
anvil run --url http://localhost:5300 scenarios/
```

## Writing New Scenarios

1. Create a YAML file in the appropriate language directory
2. Validate the syntax: `anvil validate scenarios/path/to/your-scenario.yaml`
3. Test the scenario: `anvil run scenarios/path/to/your-scenario.yaml`
4. Review the JSON report in `reports/`

### Best Practices

- **Be specific**: Detailed story descriptions produce better results
- **Start simple**: Begin with basic expectations, add more as you gain confidence
- **Use meaningful names**: Scenario names should describe what they test
- **Tag appropriately**: Tags help filter scenarios for different test runs
- **Set realistic timeouts**: Complex scenarios need more time

## Example Scenario

```yaml
name: add-user-validation
description: Add validation attributes to the User model
language: csharp
repository: c:/projects/my-app

story:
  title: Add User Model Validation
  description: |
    Add data annotation validation attributes to the User model.
    
    The User class is in src/Models/User.cs and has these properties:
    - Name (string) - required, max 100 chars
    - Email (string) - required, valid email format
    - Age (int) - required, range 0-150
    
    Add appropriate [Required], [MaxLength], [EmailAddress], 
    and [Range] attributes.

expectations:
  - type: compiles
    description: Changes should compile
  
  - type: file_contains
    description: Name should be required
    path: src/Models/User.cs
    pattern: '\[Required\].*public.*Name'
  
  - type: file_contains
    description: Email should have email validation
    path: src/Models/User.cs
    pattern: '\[EmailAddress\]'

tags:
  - validation
  - model
  - intermediate
```
