[CmdletBinding()]
param(
    [ValidateSet("Staging", "UAT", "Production")]
    [string]$Environment = "Staging"
)

$ErrorActionPreference = "Stop"

switch ($Environment) {
    "Staging"    { $BackendPort = 5500 }
    "UAT"        { $BackendPort = 6500 }
    "Production" { $BackendPort = 7500 }
}

$basePath = "C:\inetpub\wwwroot\BillBook\$Environment"
$repoPath = (Get-Item .).FullName
if (-Not (Test-Path (Join-Path $repoPath "Bill-Book.sln"))) {
    $repoPath = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
    Set-Location $repoPath
}

$backendApps = @(
    @{ Name = "accounting"; Path = "backend\Api\Accounting\Accounting.Api\Accounting.Api.csproj" },
    @{ Name = "customer"; Path = "backend\Api\Customer\Customer.Api\Customer.Api.csproj" },
    @{ Name = "inventory"; Path = "backend\Api\Inventory\Inventory.Api\Inventory.Api.csproj" },
    @{ Name = "master"; Path = "backend\Api\Master\Master.Api\Master.Api.csproj" },
    @{ Name = "purchase"; Path = "backend\Api\Purchase\Purchase.Api\Purchase.Api.csproj" },
    @{ Name = "reporting"; Path = "backend\Api\Reporting\Reporting.Api\Reporting.Api.csproj" },
    @{ Name = "sales"; Path = "backend\Api\Sales\Sales.Api\Sales.Api.csproj" }
)
$gatewayApp = @{ Name = "gateway"; Path = "backend\Gateway\Gateway.csproj" }
$frontendApps = @("web", "admin", "portal", "docs")

# Verify infrastructure exists
if (-Not (Test-Path $basePath)) {
    Write-Warning "Target directory $basePath does not exist! Please run create-iis-site.ps1 -Environment $Environment first."
    Exit 1
}

Write-Host "`n--- STOPPING APP POOLS ---"
Stop-WebAppPool -Name "BillBook_Backend_Pool_$Environment" -ErrorAction SilentlyContinue
Stop-WebAppPool -Name "BillBook_Frontend_Pool_$Environment" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "`n--- BUILDING BACKEND APIs ---"
foreach ($app in $backendApps) {
    $outPath = "$basePath\backend\$($app.Name)"
    dotnet publish $app.Path -c Release -p:EnvironmentName=$Environment -o $outPath
}

$gatewayOutPath = "$basePath\backend\$($gatewayApp.Name)"
dotnet publish $gatewayApp.Path -c Release -p:EnvironmentName=$Environment -o $gatewayOutPath

Write-Host "`n--- BUILDING FRONTEND SPAs ---"
Set-Location -Path "frontend"
foreach ($app in $frontendApps) {
    # Dynamically point the Angular App to the Backend Gateway Port for this environment
    $envName = $Environment.ToLower()
    $envFile = "$repoPath\frontend\apps\$app\src\environments\environment.$envName.ts"
    if (Test-Path $envFile) {
        $envContent = Get-Content $envFile -Raw
        $envContent = $envContent -replace "apiBaseUrl: '.*'", "apiBaseUrl: 'http://localhost:$BackendPort'"
        Set-Content -Path $envFile -Value $envContent
        Write-Host "Patched $envName API URL to Port $BackendPort for $app"
    }

    npx nx build $app -c $envName --base-href "/$app/"
    Copy-Item -Path "$repoPath\frontend\dist\apps\$app" -Destination "$basePath\frontend\$app" -Recurse -Force
    
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
          <action type="Rewrite" url="/$app/" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
"@
    Set-Content -Path "$basePath\frontend\$app\web.config" -Value $webConfigContent
}
Set-Location -Path $repoPath

Write-Host "`n--- STARTING APP POOLS ---"
Start-WebAppPool -Name "BillBook_Backend_Pool_$Environment" -ErrorAction SilentlyContinue
Start-WebAppPool -Name "BillBook_Frontend_Pool_$Environment" -ErrorAction SilentlyContinue

Write-Host "`nDEPLOYMENT COMPLETE for $Environment Environment"
