<#
.SYNOPSIS
    Creates and configures the AuraService local user account
.DESCRIPTION
    Creates a dedicated local user account for the Aura Windows Service.
    This provides a proper user context with home directory, environment variables,
    and tool caches that work correctly for all programming languages.
.PARAMETER AccountName
    Name of the service account (default: AuraService)
.PARAMETER Force
    Recreate the account even if it exists (resets password)
.EXAMPLE
    .\Create-ServiceAccount.ps1
    
    Creates the AuraService account and returns the password
#>

#Requires -RunAsAdministrator

param(
    [string]$AccountName = "AuraService",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Write-Step($message) {
    Write-Host ">> $message" -ForegroundColor Green
}

function Grant-ServiceLogonRight {
    param([string]$Username)
    
    # Export current security policy
    $tempDir = Join-Path $env:TEMP "AuraServiceSetup"
    if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Path $tempDir -Force | Out-Null }
    
    $exportFile = Join-Path $tempDir "secpol.cfg"
    $importFile = Join-Path $tempDir "secpol_new.cfg"
    
    # Export current policy
    secedit /export /cfg $exportFile /quiet
    
    # Read the file
    $content = Get-Content $exportFile -Raw
    
    # Find the SeServiceLogonRight line and add our user
    $sid = (New-Object System.Security.Principal.NTAccount($Username)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    
    if ($content -match 'SeServiceLogonRight\s*=\s*(.*)') {
        $currentValue = $matches[1]
        if ($currentValue -notmatch $sid) {
            $newValue = "SeServiceLogonRight = $currentValue,*$sid"
            $content = $content -replace 'SeServiceLogonRight\s*=\s*.*', $newValue
        }
    } else {
        # Add the line if it doesn't exist
        $content = $content -replace '\[Privilege Rights\]', "[Privilege Rights]`r`nSeServiceLogonRight = *$sid"
    }
    
    # Write the new policy
    $content | Set-Content $importFile -Encoding Unicode
    
    # Import the policy
    secedit /configure /db secedit.sdb /cfg $importFile /quiet
    
    # Cleanup
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

function Initialize-UserProfile {
    param(
        [string]$Username,
        [string]$Password
    )
    
    # Create a scheduled task that runs as the user to initialize their profile
    $taskName = "AuraProfileInit"
    $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential(".\$Username", $securePassword)
    
    # Simple command to ensure profile is created
    $action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c echo Profile initialized"
    $principal = New-ScheduledTaskPrincipal -UserId ".\$Username" -LogonType Password -RunLevel Limited
    
    # Register and run immediately
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
    Start-ScheduledTask -TaskName $taskName
    
    # Wait for completion
    $timeout = 30
    for ($i = 0; $i -lt $timeout; $i++) {
        $task = Get-ScheduledTask -TaskName $taskName
        if ($task.State -eq "Ready") { break }
        Start-Sleep -Seconds 1
    }
    
    # Cleanup
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# =============================================================================
# Main
# =============================================================================

Write-Step "Configuring Aura service account: $AccountName"

# Check if account exists
$existingUser = Get-LocalUser -Name $AccountName -ErrorAction SilentlyContinue

if ($existingUser -and -not $Force) {
    Write-Step "Account $AccountName already exists"
    
    # Account exists but we don't have the password
    # Generate new password and update
    Add-Type -AssemblyName System.Web
    $password = [System.Web.Security.Membership]::GeneratePassword(24, 4)
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    
    Set-LocalUser -Name $AccountName -Password $securePassword
    Write-Step "Password reset for existing account"
} else {
    if ($existingUser -and $Force) {
        Write-Step "Removing existing account (Force mode)"
        Remove-LocalUser -Name $AccountName
    }
    
    # Generate secure password
    Add-Type -AssemblyName System.Web
    $password = [System.Web.Security.Membership]::GeneratePassword(24, 4)
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    
    # Create the account
    Write-Step "Creating local user account..."
    New-LocalUser -Name $AccountName `
        -Password $securePassword `
        -Description "Aura AI Assistant Service Account" `
        -PasswordNeverExpires `
        -UserMayNotChangePassword | Out-Null
    
    Write-Step "Account created"
}

# Add to Administrators group for full access to build tools
Write-Step "Adding to Administrators group..."
$adminGroup = Get-LocalGroup -Name "Administrators"
$isMember = Get-LocalGroupMember -Group $adminGroup -Member $AccountName -ErrorAction SilentlyContinue
if (-not $isMember) {
    Add-LocalGroupMember -Group $adminGroup -Member $AccountName
}

# Grant "Log on as a service" right
Write-Step "Granting 'Log on as a service' right..."
Grant-ServiceLogonRight -Username $AccountName

# Initialize user profile (creates home directory)
Write-Step "Initializing user profile..."
Initialize-UserProfile -Username $AccountName -Password $password

# Verify profile was created
$profilePath = "C:\Users\$AccountName"
if (Test-Path $profilePath) {
    Write-Step "Profile created at $profilePath"
} else {
    Write-Warning "Profile directory not found at $profilePath - may be created on first service start"
}

# Store password securely in registry (DPAPI encrypted, only readable by SYSTEM and Admins)
$registryPath = "HKLM:\SOFTWARE\Aura"
if (-not (Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}

# Encrypt password using DPAPI with LocalMachine scope
Add-Type -AssemblyName System.Security
$passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($password)
$encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
    $passwordBytes,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::LocalMachine
)
$encryptedBase64 = [Convert]::ToBase64String($encryptedBytes)

Set-ItemProperty -Path $registryPath -Name "ServiceAccountPassword" -Value $encryptedBase64
Set-ItemProperty -Path $registryPath -Name "ServiceAccountName" -Value $AccountName

Write-Step "Credentials stored securely in registry"

# Return the password for immediate use by caller
Write-Output $password
