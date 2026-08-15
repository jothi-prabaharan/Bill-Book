<#
.SYNOPSIS
Creates the required IIS Application Pools for the Bill-Book system.

.DESCRIPTION
This script is a standalone utility to just create the Backend and Frontend 
Application Pools in IIS without running a full build/deployment.

.NOTES
MUST BE RUN AS ADMINISTRATOR.
#>

$ErrorActionPreference = "Stop"

if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "You do not have Administrator rights to run this script!`nPlease run as Administrator."
    Exit
}

Import-Module WebAdministration

$backendAppPool = "BillBook_Backend_Pool"
$frontendAppPool = "BillBook_Frontend_Pool"

Write-Host "--- CREATING IIS APP POOLS ---"

if (-Not (Test-Path "IIS:\AppPools\$backendAppPool")) {
    Write-Host "Creating Backend App Pool: $backendAppPool"
    New-WebAppPool -Name $backendAppPool
} else {
    Write-Host "Backend App Pool already exists."
}
Set-ItemProperty "IIS:\AppPools\$backendAppPool" -Name "managedRuntimeVersion" -Value ""
Write-Host " -> Set to 'No Managed Code'"

if (-Not (Test-Path "IIS:\AppPools\$frontendAppPool")) {
    Write-Host "Creating Frontend App Pool: $frontendAppPool"
    New-WebAppPool -Name $frontendAppPool
} else {
    Write-Host "Frontend App Pool already exists."
}
Set-ItemProperty "IIS:\AppPools\$frontendAppPool" -Name "managedRuntimeVersion" -Value ""
Write-Host " -> Set to 'No Managed Code'"

Write-Host "`nApp Pools created successfully!"
