<#
.SYNOPSIS
    Install the Aura Windows Service with a dedicated service account.
    
.DESCRIPTION
    This script is called by the Windows installer to create the AuraService
    using the credentials from Get-ServiceAccountCredential.ps1.
    
.PARAMETER InstallPath
    The installation path for Aura (e.g., C:\Program Files\Aura)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InstallPath
)

$ErrorActionPreference = 'Stop'

# Get service account credentials
$credScript = Join-Path $InstallPath "scripts\Get-ServiceAccountCredential.ps1"
$cred = & $credScript

# Create the service
$exePath = Join-Path $InstallPath "api\Aura.Api.exe"
$binPath = "`"$exePath`""

# Use sc.exe to create the service with the service account
$result = sc.exe create AuraService binPath= $binPath obj= $cred.FullUsername password= $cred.Password start= auto

if ($LASTEXITCODE -ne 0) {
    throw "Failed to create AuraService: $result"
}

Write-Host "AuraService created successfully with account: $($cred.FullUsername)"
