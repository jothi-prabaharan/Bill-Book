[CmdletBinding()]
param(
    [ValidateSet("Staging", "UAT", "Production")]
    [string]$Environment = "Staging",
    [switch]$SkipAlwaysRunning
)

$ErrorActionPreference = "Stop"

if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Please run as Administrator."
    Exit 1
}

Import-Module WebAdministration

switch ($Environment) {
    "Staging"    { $WebPort = 5200; $DocsPort = 5201; $WebsitePort = 5202; $BackendPort = 5500 }
    "UAT"        { $WebPort = 6200; $DocsPort = 6201; $WebsitePort = 6202; $BackendPort = 6500 }
    "Production" { $WebPort = 7200; $DocsPort = 7201; $WebsitePort = 7202; $BackendPort = 7500 }
}
$FrontendPort = $WebPort

$basePath = "C:\inetpub\wwwroot\BillBook\$Environment"
$backendAppPool = "BillBook_Backend_Pool_$Environment"
$frontendAppPool = "BillBook_Frontend_Pool_$Environment"

$backendApps = @(
    "accounting", "admission", "attendance", "claims", "customer", "employee", 
    "facility", "fee", "inventory", "maintenancecontract", "master", "payroll", 
    "performance", "preventive", "printing", "purchase", "recruitment", 
    "reporting", "sales", "student", "timeleave", "workorder", "worker"
)
$frontendApps = @("web", "admin", "portal", "docs", "website")

# 1. CREATE DIRECTORIES FIRST
Write-Host "`n--- PREPARING DIRECTORIES ---"
if (-Not (Test-Path $basePath)) { New-Item -ItemType Directory -Force -Path $basePath | Out-Null }
New-Item -ItemType Directory -Force -Path "$basePath\backend_root" | Out-Null
New-Item -ItemType Directory -Force -Path "$basePath\frontend_root" | Out-Null
New-Item -ItemType Directory -Force -Path "$basePath\backend\gateway" | Out-Null
foreach ($app in $backendApps) { New-Item -ItemType Directory -Force -Path "$basePath\backend\$app" | Out-Null }
foreach ($app in $frontendApps) { New-Item -ItemType Directory -Force -Path "$basePath\frontend\$app" | Out-Null }

# 2. CREATE APP POOLS
Write-Host "`n--- CREATING IIS APP POOLS ---"
function Initialize-AppPool {
    param([string]$Name, [switch]$AlwaysRunning)
    if (-Not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null; Write-Host "Created: $Name" }
    $poolPath = "IIS:\AppPools\$Name"
    Set-ItemProperty $poolPath -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty $poolPath -Name "enable32BitAppOnWin64" -Value $false
    if ($AlwaysRunning) {
        Set-ItemProperty $poolPath -Name "startMode" -Value "AlwaysRunning"
        Set-ItemProperty $poolPath -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
        Set-ItemProperty $poolPath -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::Zero)
    }
}
Initialize-AppPool -Name $backendAppPool -AlwaysRunning:(-not $SkipAlwaysRunning)
Initialize-AppPool -Name $frontendAppPool

# 3. CREATE MASTER SITES
Write-Host "`n--- CONFIGURING IIS SITES ---"
$backendSite = "BillBook_Backend_$Environment"
$frontendSite = "BillBook_Frontend_$Environment"
$docsSite = "BillBook_Docs_$Environment"
$websiteSite = "BillBook_Website_$Environment"

if (Test-Path "IIS:\Sites\$backendSite") { Remove-WebSite -Name $backendSite }
if (Test-Path "IIS:\Sites\$frontendSite") { Remove-WebSite -Name $frontendSite }
if (Test-Path "IIS:\Sites\$docsSite") { Remove-WebSite -Name $docsSite }
if (Test-Path "IIS:\Sites\$websiteSite") { Remove-WebSite -Name $websiteSite }

New-WebSite -Name $backendSite -Port $BackendPort -PhysicalPath "$basePath\backend\gateway" -ApplicationPool $backendAppPool | Out-Null
New-WebSite -Name $frontendSite -Port $WebPort -PhysicalPath "$basePath\frontend_root" -ApplicationPool $frontendAppPool | Out-Null
New-WebSite -Name $docsSite -Port $DocsPort -PhysicalPath "$basePath\frontend\docs" -ApplicationPool $frontendAppPool | Out-Null
New-WebSite -Name $websiteSite -Port $WebsitePort -PhysicalPath "$basePath\frontend\website" -ApplicationPool $frontendAppPool | Out-Null

Write-Host "Created Site: $backendSite (Port $BackendPort)"
Write-Host "Created Site: $frontendSite (Port $WebPort)"
Write-Host "Created Site: $docsSite (Port $DocsPort)"
Write-Host "Created Site: $websiteSite (Port $WebsitePort)"

# 4. CREATE VIRTUAL APPLICATIONS
Write-Host "`n--- CONFIGURING VIRTUAL APPLICATIONS ---"
foreach ($app in $backendApps) {
    New-WebApplication -Site $backendSite -Name $app -PhysicalPath "$basePath\backend\$app" -ApplicationPool $backendAppPool | Out-Null
    Write-Host "Created Virtual App: /$app (Backend)"
}
foreach ($app in $frontendApps) {
    New-WebApplication -Site $frontendSite -Name $app -PhysicalPath "$basePath\frontend\$app" -ApplicationPool $frontendAppPool | Out-Null
    Write-Host "Created Virtual App: /$app (Frontend)"
}

# 5. FIREWALL RULES
Write-Host "`n--- CONFIGURING FIREWALL ---"
$firewallPorts = @(
    @{ Name = "BillBook_Backend_$BackendPort"; Port = $BackendPort },
    @{ Name = "BillBook_Web_$WebPort"; Port = $WebPort },
    @{ Name = "BillBook_Docs_$DocsPort"; Port = $DocsPort },
    @{ Name = "BillBook_Website_$WebsitePort"; Port = $WebsitePort }
)

foreach ($fw in $firewallPorts) {
    if (-Not (Get-NetFirewallRule -DisplayName $fw.Name -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $fw.Name -Direction Inbound -LocalPort $fw.Port -Protocol TCP -Action Allow | Out-Null
        Write-Host "Opened Port $($fw.Port)"
    }
}

Write-Host "`nIIS SETUP COMPLETE for $Environment!"

