<#
.SYNOPSIS
Deploys the Bill-Book application locally to IIS.

.DESCRIPTION
This script builds all backend and frontend projects, creates IIS application pools,
provisions the necessary websites with correct port bindings, and opens Windows Firewall ports.

.NOTES
MUST BE RUN AS ADMINISTRATOR.
#>

$ErrorActionPreference = "Stop"

# 1. Prerequisite Check (Elevation)
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "You do not have Administrator rights to run this script!`nPlease right-click PowerShell and select 'Run as Administrator', then execute this script again."
    Exit
}

# Ensure WebAdministration module is available
Import-Module WebAdministration

# 2. Configuration
$basePath = "C:\inetpub\wwwroot\BillBook"
$repoPath = (Get-Item .).FullName

# Make sure we are in the repository root (in case script was run from somewhere else)
if (-Not (Test-Path (Join-Path $repoPath "Bill-Book.sln"))) {
    $repoPath = (Split-Path -Parent $MyInvocation.MyCommand.Path)
    $repoPath = (Split-Path -Parent $repoPath) # Go up from /scripts to root
    Set-Location $repoPath
}

$backendApps = @(
    @{ Name = "Gateway"; Path = "backend\Gateway\Gateway.csproj"; Port = 5000 },
    @{ Name = "Master"; Path = "backend\Api\Master\Master.Api\Master.Api.csproj"; Port = 5003 },
    @{ Name = "Inventory"; Path = "backend\Api\Inventory\Inventory.Api\Inventory.Api.csproj"; Port = 5004 },
    @{ Name = "Accounting"; Path = "backend\Api\Accounting\Accounting.Api\Accounting.Api.csproj"; Port = 5006 },
    @{ Name = "Customer"; Path = "backend\Api\Customer\Customer.Api\Customer.Api.csproj"; Port = 5008 },
    @{ Name = "Reporting"; Path = "backend\Api\Reporting\Reporting.Api\Reporting.Api.csproj"; Port = 5009 },
    @{ Name = "Sales"; Path = "backend\Api\Sales\Sales.Api\Sales.Api.csproj"; Port = 5011 },
    @{ Name = "Purchase"; Path = "backend\Api\Purchase\Purchase.Api\Purchase.Api.csproj"; Port = 5012 }
)

$frontendApps = @(
    @{ Name = "web"; Port = 80 },
    # @{ Name = "admin"; Port = 81 },
    # @{ Name = "portal"; Port = 82 },
    @{ Name = "docs"; Port = 83 }
)

$backendAppPool = "BillBook_Backend_Pool"
$frontendAppPool = "BillBook_Frontend_Pool"

# 3. Clean Publish Directory
Write-Host "`n--- PREPARING DIRECTORIES ---"

# Stop App Pools if they exist to prevent file locking during deletion
if (Test-Path "IIS:\AppPools\$backendAppPool") { Stop-WebAppPool -Name $backendAppPool -ErrorAction SilentlyContinue }
if (Test-Path "IIS:\AppPools\$frontendAppPool") { Stop-WebAppPool -Name $frontendAppPool -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2

Write-Host "Publish directory: $basePath"
if (Test-Path $basePath) {
    Remove-Item -Recurse -Force $basePath
}
New-Item -ItemType Directory -Force -Path $basePath | Out-Null

# 4. Build and Publish Backend
Write-Host "`n--- BUILDING BACKEND APIs ---"
foreach ($app in $backendApps) {
    $outPath = Join-Path $basePath "backend\$($app.Name)"
    Write-Host "Publishing $($app.Name) to $outPath..."
    dotnet publish $app.Path -c Release -o $outPath
}

# 5. Build and Publish Frontend
Write-Host "`n--- BUILDING FRONTEND SPAs ---"
Set-Location -Path "frontend"
foreach ($app in $frontendApps) {
    Write-Host "Building frontend: $($app.Name)..."
    npx nx build $app.Name
    
    $outPath = Join-Path $basePath "frontend\$($app.Name)"
    $distPath = Join-Path $repoPath "frontend\dist\apps\$($app.Name)"
    
    # Copy build output to IIS folder
    Copy-Item -Path $distPath -Destination $outPath -Recurse -Force
}
Set-Location -Path $repoPath

# 6. Verify IIS App Pools Exist
Write-Host "`n--- VERIFYING IIS APP POOLS ---"
if (-Not (Test-Path "IIS:\AppPools\$backendAppPool")) {
    Write-Warning "Backend App Pool ($backendAppPool) does not exist! Please run create-iis-pools.ps1 first."
    Exit
}
if (-Not (Test-Path "IIS:\AppPools\$frontendAppPool")) {
    Write-Warning "Frontend App Pool ($frontendAppPool) does not exist! Please run create-iis-pools.ps1 first."
    Exit
}

# 7. Configure IIS Sites and Firewall Rules
Write-Host "`n--- CONFIGURING IIS SITES & FIREWALL ---"

function Setup-Site {
    param ($SiteName, $Port, $PhysicalPath, $AppPool)
    
    $fullSiteName = "BillBook_$SiteName"
    
    # Remove existing site if present
    if (Test-Path "IIS:\Sites\$fullSiteName") {
        Remove-WebSite -Name $fullSiteName
    }
    
    Write-Host "Creating IIS Site: $fullSiteName on Port $Port -> $PhysicalPath"
    New-WebSite -Name $fullSiteName -Port $Port -PhysicalPath $PhysicalPath -ApplicationPool $AppPool | Out-Null

    # Firewall Rule
    $fwRuleName = "BillBook_Port_$Port"
    if (-Not (Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue)) {
        Write-Host "  -> Opening Windows Firewall for Port $Port"
        New-NetFirewallRule -DisplayName $fwRuleName -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow | Out-Null
    }
}

# Setup Backend Sites
foreach ($app in $backendApps) {
    $path = Join-Path $basePath "backend\$($app.Name)"
    Setup-Site -SiteName $app.Name -Port $app.Port -PhysicalPath $path -AppPool $backendAppPool
}

# Setup Frontend Sites
foreach ($app in $frontendApps) {
    $path = Join-Path $basePath "frontend\$($app.Name)"
    Setup-Site -SiteName "Frontend_$($app.Name)" -Port $app.Port -PhysicalPath $path -AppPool $frontendAppPool
    
    # Create web.config for Angular SPA URL Rewrite
    $webConfigPath = Join-Path $path "web.config"
    $webConfigContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="AngularJS Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
"@
    Set-Content -Path $webConfigPath -Value $webConfigContent
}

Write-Host "`n--- STARTING APP POOLS ---"
Start-WebAppPool -Name $backendAppPool -ErrorAction SilentlyContinue
Start-WebAppPool -Name $frontendAppPool -ErrorAction SilentlyContinue

Write-Host "`n======================================================="
Write-Host "DEPLOYMENT COMPLETE!"
Write-Host "The Gateway is accessible at: http://localhost:5000"
Write-Host "The Main Web App is accessible at: http://localhost:80"
Write-Host "======================================================="
