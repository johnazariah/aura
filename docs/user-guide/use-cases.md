# Aura Use Cases for Developers

This guide shows practical examples of how to use Aura for common development tasks. Each use case includes step-by-step instructions and tips for getting the best results.

## Table of Contents

- [Understanding Your Codebase](#understanding-your-codebase)
- [Writing New Features](#writing-new-features)
- [Writing Tests](#writing-tests)
- [Refactoring Code](#refactoring-code)
- [Documentation](#documentation)
- [Bug Investigation](#bug-investigation)
- [Code Review Assistance](#code-review-assistance)
- [Learning a New Codebase](#learning-a-new-codebase)

---

## Understanding Your Codebase

### Use Case: "How does X work in this project?"

Use Aura's code-aware chat to understand unfamiliar code.

**Steps:**
1. Open the Aura panel in VS Code
2. Click the **Chat** tab
3. Ask your question with context

**Example Questions:**
```
How does authentication work in this project?
```
```
What's the flow when a user places an order?
```
```
Where is the database connection configured?
```

**Tips:**
- Be specific about what you want to understand
- Reference class or file names if you know them
- Ask follow-up questions to drill deeper

### Use Case: "Find all code related to X"

Use semantic search to find relevant code even without knowing exact names.

**Steps:**
1. Open Aura Chat
2. Ask where functionality is implemented

**Example:**
```
Where is email sending implemented?
```

Aura will return:
- File paths and line numbers
- Function/class names
- How they relate to each other

---

## Writing New Features

### Use Case: "Implement a new endpoint/feature"

Use workflows for multi-file feature implementation.

**Steps:**
1. Click **+ New Workflow** in Aura panel
2. Describe what you want to build
3. Let Aura analyze your codebase and create a plan
4. Review and approve each step

**Good Workflow Descriptions:**

```
Create a REST endpoint POST /api/users/{id}/avatar that accepts 
an image upload, validates it's under 5MB, and stores it in Azure 
Blob Storage. Return the URL of the uploaded image.
```

```
Add a "favorites" feature where users can favorite products. 
Create the database table, repository, service, and API endpoints 
for add/remove/list favorites.
```

**Tips:**
- Reference existing patterns: "following the same structure as OrderController"
- Specify constraints: "use async/await throughout"
- Mention frameworks: "use FluentValidation for input validation"

### Use Case: "Create a new class following project patterns"

Let Aura analyze existing code to match your conventions.

**Workflow Description:**
```
Create a new ShippingService class following the same patterns 
as PaymentService. It should have methods for:
- CalculateShippingCost(Order order)
- GetShippingOptions(Address destination)
- TrackShipment(string trackingNumber)
```

Aura will:
1. Analyze PaymentService structure
2. Match dependency injection patterns
3. Follow naming conventions
4. Use appropriate interfaces

---

## Writing Tests

### Use Case: "Generate unit tests for existing code"

Create comprehensive test coverage for your code.

**Steps:**
1. Create a new workflow
2. Describe what needs testing

**Workflow Description:**
```
Write unit tests for the OrderService class. Cover all public 
methods including edge cases. Use xUnit and Moq following 
the existing test patterns in the project.
```

**Tips:**
- Specify the test framework (xUnit, NUnit, Jest, pytest, etc.)
- Reference existing tests: "follow the patterns in UserServiceTests.cs"
- Mention coverage goals: "cover all public methods"

### Use Case: "Add tests for a specific scenario"

Target specific functionality or edge cases.

**Workflow Description:**
```
Add tests to OrderServiceTests for the scenario where a user 
tries to place an order with an expired payment method. 
Should test that appropriate exceptions are thrown and 
no order is created.
```

---

## Refactoring Code

### Use Case: "Extract a class/method"

Break down large files into smaller, focused units.

**Workflow Description:**
```
The UserService class is too large (800+ lines). Extract the 
email-related functionality into a new EmailNotificationService 
class. Update UserService to depend on the new service.
```

### Use Case: "Modernize legacy patterns"

Update old code to use modern idioms.

**Workflow Description:**
```
Refactor the PaymentProcessor class to use async/await 
instead of the current callback-based approach. Ensure 
all calling code is updated appropriately.
```

### Use Case: "Apply a pattern consistently"

Standardize code across your project.

**Workflow Description:**
```
All repository classes should implement the IDisposable pattern 
like OrderRepository does. Update CustomerRepository, 
ProductRepository, and InventoryRepository to match.
```

---

## Documentation

### Use Case: "Generate API documentation"

Create documentation from your code.

**Workflow Description:**
```
Generate XML documentation comments for all public methods 
in the OrderController class. Include parameter descriptions, 
return value documentation, and example usage where appropriate.
```

### Use Case: "Create a README for a component"

Document how a subsystem works.

**Workflow Description:**
```
Create a README.md for the /src/Payments directory explaining:
- What the payment module does
- Key classes and their responsibilities
- How to add a new payment provider
- Configuration options
```

### Use Case: "Document architecture decisions"

Capture why code is structured a certain way.

**Chat Question:**
```
Explain the architecture of the order processing system 
and why it's designed this way. I want to document this 
for new team members.
```

---

## Bug Investigation

### Use Case: "Understand a bug's root cause"

Use chat to trace through code paths.

**Chat Questions:**
```
Trace the code path when a user submits an order. 
What validations happen and in what order?
```
```
What could cause the OrderProcessor to throw a 
NullReferenceException on line 145?
```

### Use Case: "Find similar patterns that might have the same bug"

Identify code that might have the same issue.

**Chat Question:**
```
Show me all places where we access user.Address without 
null checking first.
```

### Use Case: "Implement a bug fix"

Create a workflow to fix an issue.

**Workflow Description:**
```
Fix: Users can submit orders with negative quantities.

Add validation in OrderValidator to reject line items 
where Quantity <= 0. Add a unit test that verifies 
negative quantities are rejected.
```

---

## Code Review Assistance

### Use Case: "Review code for issues"

Get a second opinion on code quality.

**Chat Question:**
```
Review the PaymentService class for potential issues:
- Thread safety concerns
- Error handling gaps
- Performance problems
- Code smells
```

### Use Case: "Check for security issues"

Identify potential vulnerabilities.

**Chat Question:**
```
Are there any SQL injection vulnerabilities in the 
UserRepository class?
```
```
Review the authentication flow for security issues.
```

### Use Case: "Suggest improvements"

Get recommendations for better code.

**Chat Question:**
```
How could the OrderProcessor class be improved? 
Consider testability, maintainability, and performance.
```

---

## Learning a New Codebase

### Use Case: "Get a high-level overview"

Understand project structure quickly.

**Chat Questions:**
```
Give me a high-level overview of this codebase. 
What are the main components and how do they interact?
```
```
What's the tech stack of this project?
```

### Use Case: "Understand coding conventions"

Learn how the team writes code.

**Chat Questions:**
```
What patterns are used for error handling in this project?
```
```
How is logging typically done? Show me some examples.
```
```
What naming conventions are used for tests?
```

### Use Case: "Find entry points"

Locate where execution starts.

**Chat Questions:**
```
What are the main entry points into the application?
```
```
How does a request flow from the API endpoint to the database?
```

---

## Best Practices

### For Workflows

1. **Be specific** - Include details about what you want
2. **Reference patterns** - Point to existing code to follow
3. **Specify constraints** - Mention frameworks, styles, requirements
4. **Review carefully** - Check each step before approving
5. **Use step chat** - Ask agents to refine their work

### For Chat

1. **Index first** - Ensure your codebase is indexed
2. **Ask follow-ups** - Drill down into specifics
3. **Reference files** - Mention specific files when relevant
4. **Be conversational** - Aura remembers context within a session

### For Code Quality

1. **Run tests after workflows** - Verify changes work
2. **Review diffs** - Check exactly what changed
3. **Use Git history** - Aura creates commits you can revert
4. **Iterate** - Use chat to refine workflow outputs

---

## Supported Languages

Aura provides full support for these languages:

| Language | Indexing | Workflows | Specialist Agent |
|----------|----------|-----------|------------------|
| C# | ✅ Full (Roslyn) | ✅ | ✅ RoslynCodingAgent |
| TypeScript | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| JavaScript | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| Python | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| Go | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| Rust | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| Java | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |
| F# | ✅ TreeSitter | ✅ | ✅ LanguageSpecialist |

Other languages use the generic coding agent which handles most languages well.

---

## Next Steps

- [Workflows Guide](workflows.md) - Detailed workflow documentation
- [Chat Guide](chat.md) - Advanced chat features
- [Indexing Guide](indexing.md) - Code indexing details
- [LLM Configuration](../configuration/llm-providers.md) - Configure AI providers
