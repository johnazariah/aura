<#
.SYNOPSIS
    Retrieves the AuraService account credentials
.DESCRIPTION
    Reads the encrypted service account password from the registry.
    Must be run as Administrator.
.EXAMPLE
    $creds = .\Get-ServiceAccountCredential.ps1
    $creds.Username
    $creds.Password
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$registryPath = "HKLM:\SOFTWARE\Aura"

if (-not (Test-Path $registryPath)) {
    throw "Aura registry key not found. Run Create-ServiceAccount.ps1 first."
}

$accountName = Get-ItemProperty -Path $registryPath -Name "ServiceAccountName" -ErrorAction SilentlyContinue
$encryptedPassword = Get-ItemProperty -Path $registryPath -Name "ServiceAccountPassword" -ErrorAction SilentlyContinue

if (-not $accountName -or -not $encryptedPassword) {
    throw "Service account credentials not found in registry. Run Create-ServiceAccount.ps1 first."
}

# Decrypt password using DPAPI
Add-Type -AssemblyName System.Security
$encryptedBytes = [Convert]::FromBase64String($encryptedPassword.ServiceAccountPassword)
$decryptedBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
    $encryptedBytes,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::LocalMachine
)
$password = [System.Text.Encoding]::UTF8.GetString($decryptedBytes)

# Return as object
[PSCustomObject]@{
    Username = $accountName.ServiceAccountName
    Password = $password
    FullUsername = ".\$($accountName.ServiceAccountName)"
}
