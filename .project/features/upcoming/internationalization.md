# Internationalization (i18n) & Localization (l10n)

## Overview

Aura should support multiple languages to reach a global audience. This spec outlines the approach for internationalizing the codebase and providing localized content.

## Goals

1. **User-facing text** should be localizable (extension UI, error messages)
2. **Configuration labels** should adapt to user's VS Code language
3. **Date, time, and number formats** should respect locale
4. **Developer experience** should not suffer - English remains the development language

## Non-Goals

- Translating agent markdown files (these are user-created)
- Localizing log messages (kept in English for debugging/support)
- Right-to-left (RTL) layout support (future consideration)

## Priority Languages

| Priority | Language | Code | Rationale |
|----------|----------|------|-----------|
| 1 | English | `en` | Default/development language |
| 2 | Norwegian Bokmål | `nb` | Primary non-English market |
| 3 | Norwegian Nynorsk | `nn` | Norwegian variant |
| 4 | German | `de` | Large developer community |
| 5 | French | `fr` | Large developer community |

## Components to Localize

### VS Code Extension (High Priority)

#### package.json / package.nls.json

Commands, configuration, and view titles.

```
extension/
├── package.json              # Uses %keys%
├── package.nls.json          # English (default)
├── package.nls.nb.json       # Norwegian Bokmål
└── package.nls.nn.json       # Norwegian Nynorsk
```

**Example package.json:**
```json
{
  "commands": [{
    "command": "aura.createWorkflow",
    "title": "%command.createWorkflow%"
  }]
}
```

**Example package.nls.json:**
```json
{
  "command.createWorkflow": "Create Workflow",
  "config.apiUrl.description": "URL for the Aura API"
}
```

**Example package.nls.nb.json:**
```json
{
  "command.createWorkflow": "Opprett arbeidsflyt",
  "config.apiUrl.description": "URL for Aura API-et"
}
```

#### Runtime Strings (l10n bundle)

For strings in TypeScript code (webviews, notifications, tree items).

```
extension/
└── l10n/
    ├── bundle.l10n.json       # English (default)
    ├── bundle.l10n.nb.json    # Norwegian Bokmål
    └── bundle.l10n.de.json    # German
```

**Usage in code:**
```typescript
import * as vscode from 'vscode';

// Before
vscode.window.showInformationMessage('Workflow created successfully');

// After
vscode.window.showInformationMessage(vscode.l10n.t('Workflow created successfully'));
```

#### Webview HTML Content

Webviews (workflow panel, chat) need special handling.

**Option A: Template substitution**
```typescript
const html = `<h1>${vscode.l10n.t('Create New Workflow')}</h1>`;
```

**Option B: Pass translations to webview**
```typescript
panel.webview.postMessage({
  type: 'setTranslations',
  translations: {
    title: vscode.l10n.t('Create New Workflow'),
    submit: vscode.l10n.t('Create'),
    cancel: vscode.l10n.t('Cancel')
  }
});
```

### .NET API (Medium Priority)

Use `IStringLocalizer<T>` with resource files.

```
src/Aura.Foundation/
└── Resources/
    ├── Messages.resx           # English (default)
    ├── Messages.nb.resx        # Norwegian Bokmål
    └── Messages.de.resx        # German
```

**Usage:**
```csharp
public class WorkflowService
{
    private readonly IStringLocalizer<Messages> _localizer;
    
    public WorkflowService(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }
    
    public Result<Workflow> Create(...)
    {
        if (string.IsNullOrEmpty(title))
            return Result.Failure<Workflow>(_localizer["TitleRequired"]);
    }
}
```

### Configuration Keys

Some configuration values are user-visible (like `BranchPrefix`). These should have localized descriptions but the values themselves remain language-neutral.

```json
{
  "Aura:Modules:Developer:BranchPrefix": {
    "value": "workflow",
    "description": "%config.branchPrefix.description%"
  }
}
```

## Implementation Phases

### Phase 1: Infrastructure (Foundation)

- [ ] Add `@vscode/l10n` dependency to extension
- [ ] Create `l10n/bundle.l10n.json` with extracted strings
- [ ] Create `package.nls.json` for package.json strings
- [ ] Update `package.json` to use `%key%` syntax
- [ ] Add localization to build/packaging process

### Phase 2: Extract Strings (Extension)

- [ ] Extract webview HTML strings to template variables
- [ ] Replace hardcoded strings in TypeScript with `vscode.l10n.t()`
- [ ] Extract error messages and notifications
- [ ] Extract tree view labels and tooltips

### Phase 3: Extract Strings (API)

- [ ] Add `Microsoft.Extensions.Localization` package
- [ ] Create `Resources/Messages.resx`
- [ ] Replace hardcoded error messages with `IStringLocalizer`
- [ ] Configure request culture provider for API

### Phase 4: First Translation (Norwegian)

- [ ] Create `package.nls.nb.json`
- [ ] Create `bundle.l10n.nb.json`
- [ ] Create `Messages.nb.resx`
- [ ] Test with VS Code language set to Norwegian

### Phase 5: Community Translations

- [ ] Document translation contribution process
- [ ] Set up translation management (Crowdin, Weblate, or manual)
- [ ] Add German (`de`) and French (`fr`) translations

## String Extraction Guidelines

### Do Localize

- User-visible UI text
- Error messages shown to users
- Notification messages
- Button labels
- Form labels and placeholders
- Tooltips
- Status messages

### Do Not Localize

- Log messages (keep in English for support)
- Internal error codes
- API endpoint paths
- Configuration key names
- Git branch names
- Technical identifiers

## Date/Time/Number Formatting

### Extension (TypeScript)

```typescript
// Use Intl API
const date = new Date();
const formatted = new Intl.DateTimeFormat(vscode.env.language).format(date);
```

### API (.NET)

```csharp
// Use request culture
var culture = CultureInfo.CurrentCulture;
var formatted = date.ToString("d", culture);
```

## Testing

### Manual Testing

1. Set VS Code language: `Ctrl+Shift+P` → "Configure Display Language"
2. Verify all UI elements display in selected language
3. Check date/time formatting
4. Verify fallback to English for missing translations

### Automated Testing

```typescript
// Test that all keys have translations
describe('l10n', () => {
  it('should have all keys in Norwegian bundle', () => {
    const en = require('../l10n/bundle.l10n.json');
    const nb = require('../l10n/bundle.l10n.nb.json');
    
    for (const key of Object.keys(en)) {
      expect(nb[key]).toBeDefined();
    }
  });
});
```

## File Structure (Final)

```
extension/
├── package.json
├── package.nls.json            # English
├── package.nls.nb.json         # Norwegian Bokmål
├── package.nls.nn.json         # Norwegian Nynorsk
├── package.nls.de.json         # German
├── package.nls.fr.json         # French
└── l10n/
    ├── bundle.l10n.json        # English
    ├── bundle.l10n.nb.json
    ├── bundle.l10n.nn.json
    ├── bundle.l10n.de.json
    └── bundle.l10n.fr.json

src/Aura.Foundation/
└── Resources/
    ├── Messages.resx           # English
    ├── Messages.nb.resx
    ├── Messages.de.resx
    └── Messages.fr.resx
```

## References

- [VS Code Localization](https://code.visualstudio.com/api/references/vscode-api#l10n)
- [vscode-l10n](https://github.com/microsoft/vscode-l10n)
- [.NET Localization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization)
- [ICU Message Format](https://unicode-org.github.io/icu/userguide/format_parse/messages/)

## Status

**Status:** Not Started  
**Priority:** Medium  
**Estimated Effort:** 2-3 weeks for full implementation
