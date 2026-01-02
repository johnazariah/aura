# Code-Aware Chat

Aura's chat feature lets you have conversations about your codebase with full context awareness.

## Opening Chat

### From VS Code

1. Click **Aura** in the Activity Bar
2. Click **"Chat"** tab
3. Start typing your question

### From Command Palette

1. Press `Ctrl+Shift+P`
2. Type "Aura: Open Chat"

### Keyboard Shortcut

Set up a keyboard shortcut in VS Code settings for quick access.

## What Makes Chat "Code-Aware"?

Unlike generic AI chat, Aura chat:

- **Knows your code structure** - Classes, functions, relationships
- **Searches semantically** - Finds relevant code even without exact keywords
- **Respects context** - Understands your current file and selection

## Example Conversations

### Understanding Code

```text
You: What does the PaymentService class do?

Aura: The PaymentService handles payment processing for orders. 
It has three main methods:

1. `ProcessPayment(Order order)` - Charges the customer's card
2. `RefundPayment(string transactionId)` - Issues refunds
3. `ValidateCard(CardDetails card)` - Verifies card before processing

It uses the StripeClient for actual payment operations and logs
all transactions to the PaymentAuditRepository.
```

### Finding Code

```text
You: Where is user authentication implemented?

Aura: Authentication is implemented in several places:

1. **AuthController.cs** (line 25-89) - Login/logout endpoints
2. **JwtService.cs** (line 12-67) - Token generation and validation
3. **AuthMiddleware.cs** (line 8-45) - Request authentication

The main flow is:
1. User calls POST /api/auth/login
2. AuthController validates credentials via UserRepository
3. JwtService generates a token
4. Subsequent requests are validated by AuthMiddleware
```

### Explaining Patterns

```text
You: How does dependency injection work in this project?

Aura: This project uses .NET's built-in DI container configured 
in Program.cs. Here's the pattern:

1. Services are registered in `ConfigureServices()`:
   - `services.AddScoped<IUserService, UserService>()`
   
2. Controllers receive dependencies via constructor:
   - `public UserController(IUserService userService)`

3. Scoped services are created per-request

Key registration locations:
- Core services: Program.cs line 45-78
- Module services: DeveloperModule.cs line 23-56
```

## Using Current Context

### With Selection

1. Select code in the editor
2. Open chat
3. Ask about the selection

```text
You: [with code selected] What does this method do?

Aura: The selected `CalculateDiscount` method applies discount 
rules in this order:

1. Checks for member discount (10%)
2. Applies quantity discount (5% for 10+ items)
3. Stacks promotional codes if valid

The maximum total discount is capped at 30%.
```

### With Current File

Chat automatically knows which file you're viewing:

```text
You: Are there any bugs in this file?

Aura: I found a few potential issues in OrderService.cs:

1. **Line 45**: Null check missing before accessing order.Customer
2. **Line 78**: Exception is caught but not logged
3. **Line 112**: Possible race condition in concurrent updates

Would you like me to create a workflow to fix these?
```

## Chat Commands

Use special commands for specific actions:

| Command | Description |
|---------|-------------|

| `/explain` | Explain selected code in detail |
| `/find` | Search for code matching description |
| `/review` | Review code for issues |
| `/test` | Suggest tests for code |
| `/docs` | Generate documentation |

### Example

```text
You: /review the authentication flow

Aura: Code Review: Authentication Flow

✅ Good:
- JWT tokens properly signed
- Passwords hashed with bcrypt
- Token expiration configured

⚠️ Concerns:
- No rate limiting on login attempts
- Refresh tokens not implemented
- Token stored in localStorage (XSS risk)

🔧 Recommendations:
1. Add rate limiting middleware
2. Implement refresh token rotation
3. Use httpOnly cookies for tokens
```

## From Chat to Workflow

If chat suggests code changes, you can create a workflow:

```text
You: Add rate limiting to the login endpoint

Aura: I recommend adding rate limiting using the 
AspNetCoreRateLimit package. This would involve:

1. Install AspNetCoreRateLimit package
2. Configure rate limit rules
3. Add middleware to Program.cs
4. Apply to AuthController

[Create Workflow] [Show Code] [Explain More]
```

Click **"Create Workflow"** to turn the suggestion into executable steps.

## Tips for Effective Chat

### Be Specific

```text
❌ "How does this work?"
✅ "How does the order validation logic handle partial refunds?"
```

### Provide Context

```text
❌ "Why is this slow?"
✅ "The GetUserOrders method on line 45 is slow for users 
    with 1000+ orders. Why might this be?"
```

### Ask Follow-ups

Chat remembers context within a session:

```text
You: What caching is used in the project?
Aura: The project uses Redis caching via...

You: How would I add caching to the ProductService?
Aura: Based on the existing Redis setup, you would...
```

## Privacy

All chat processing happens:

- **Locally** with Ollama (default)
- **Or** via configured cloud providers

Your code never leaves your control unless you explicitly configure cloud LLM providers.
