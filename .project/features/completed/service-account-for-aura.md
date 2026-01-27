# Dedicated Service Account for Aura

**Status:** ✅ Complete
**Completed:** 2026-01-28
**Created:** 2026-01-25

## Overview

The Aura Windows Service runs under a dedicated `AuraService` local user account instead of LocalSystem. This provides a proper user context with home directory, environment variables, and tool caches that work correctly for all programming languages.

## Problem Statement

The Aura Windows Service previously ran as LocalSystem, which caused:
1. **No user profile** - LocalSystem has no home directory, no user-level config
2. **No NuGet cache access** - Can't use cached packages from user profile
3. **No workloads** - .NET workloads (Aspire, MAUI, etc.) are user-scoped
4. **Different PATH** - May not have access to user-installed tools

This forced workarounds for every environment difference.

## Solution

Create a local `AuraService` user account during installation that:
- Has a real user profile (`C:\Users\AuraService`)
- Works with all language toolchains (Python, Go, Node, .NET, Rust, etc.)
- Has admin rights for installing tools
- Has "Log on as a service" permission

## Implementation

### Scripts Created

1. **`scripts/Create-ServiceAccount.ps1`** - Creates the AuraService local account
   - Generates random 24-char password
   - Adds to Administrators group
   - Grants "Log on as a service" right via secedit
   - Initializes user profile via scheduled task
   - Stores encrypted password in registry (DPAPI)

2. **`scripts/Get-ServiceAccountCredential.ps1`** - Retrieves stored credentials
   - Decrypts password from registry
   - Returns username/password for service registration

### Installer Integration

- `installers/windows/Aura.iss` - Runs Create-ServiceAccount.ps1 before service creation
- Service registered with `obj= AuraService` credentials

### Security

| Aspect | Dedicated Account |
|--------|-------------------|
| Privileges | Admin (required for tooling) |
| Network access | User account |
| Profile isolation | Dedicated user directory |
| Password | Random 24-char, DPAPI encrypted in registry |

## Files Changed

- `scripts/Create-ServiceAccount.ps1` (new)
- `scripts/Get-ServiceAccountCredential.ps1` (new)
- `installers/windows/Aura.iss` (service registration)
- `installers/windows/Diagnose-Aura.ps1` (account checks)
