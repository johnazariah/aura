# Dedicated Service Account for Aura

**Status:** � In Progress
**Created:** 2026-01-25

## Problem Statement

The Aura Windows Service currently runs as LocalSystem, which causes:
1. **No user profile** - LocalSystem has no home directory, no user-level config
2. **No NuGet cache access** - Can't use cached packages from user profile
3. **No workloads** - .NET workloads (Aspire, MAUI, etc.) are user-scoped
4. **Different PATH** - May not have access to user-installed tools

This forces us to create workarounds for every environment difference.

## Solution: Dedicated Service Account

Create a local `AuraService` user account during installation that:
- Has a real user profile (`C:\Users\AuraService`)
- Works with all language toolchains (Python, Go, Node, .NET, Rust, etc.)
- Has admin rights for installing tools
- Has "Log on as a service" permission

## Implementation

### New Scripts

1. **`scripts/Create-ServiceAccount.ps1`** - Creates the AuraService local account
   - Generates random 24-char password
   - Adds to Administrators group
   - Grants "Log on as a service" right via secedit
   - Initializes user profile via scheduled task
   - Stores encrypted password in registry (DPAPI)

2. **`scripts/Get-ServiceAccountCredential.ps1`** - Retrieves stored credentials
   - Decrypts password from registry
   - Returns username/password for service registration

### Installer Changes

- `installers/windows/Aura.iss` - Runs Create-ServiceAccount.ps1 before service creation
- `scripts/Publish-Release.ps1` - Includes new scripts in publish output

### Update-LocalInstall.ps1 Changes

- Creates AuraService account if missing
- Migrates existing LocalSystem services to AuraService account
- No longer installs workloads (not needed with proper user context)

### QualityGateService.cs Changes

- Simplified restore command (no shared cache path needed)
- Service runs with normal user context, NuGet cache works naturally

## Security Considerations

| Aspect | LocalSystem | Dedicated Account |
|--------|-------------|-------------------|
| Privileges | Full admin | Standard user |
| Network access | Machine account | User account |
| Profile isolation | Shared system | Dedicated user |
| Attack surface | High (admin) | Lower (user) |
| Password | None | Random, stored securely |

## Alternatives Considered

### 1. Virtual Service Account (MSA/gMSA)
- Managed by Windows, no password management
- Requires domain controller for gMSA
- Standalone MSA (sMSA) works on standalone machines
- More complex to set up

### 2. NetworkService
- Similar limitations to LocalSystem
- No user profile
- Doesn't solve the core problem

### 3. Machine-wide Workload Installation
- Current workaround approach
- Requires admin rights for every workload
- Doesn't scale to user projects

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Password storage | Use DPAPI to encrypt, store in registry |
| Profile disk usage | Document expected usage, cleanup on uninstall |
| Upgrade complexity | Account persists across upgrades |
| Permission issues | Test thoroughly on clean machines |

## Testing Plan

1. Clean Windows VM
2. Install Aura
3. Verify service starts with AuraService account
4. Run story with Aspire project
5. Verify NuGet restore works
6. Verify workload resolution works
7. Uninstall and verify account cleanup

## Open Questions

1. Should we delete the account on uninstall, or leave it (safer for upgrades)?
2. How to handle existing LocalSystem installations during upgrade?
3. Should the password be visible to the user (for debugging)?
4. Do we need the Carbon PowerShell module for granting service logon rights, or can we use built-in secedit?

## References

- [Service User Accounts](https://learn.microsoft.com/en-us/windows/win32/services/service-user-accounts)
- [Managed Service Accounts](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/understand-service-accounts)
- [Granting Log On As Service Right](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-security/grant-users-rights-manage-services)
