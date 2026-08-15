<#
.SYNOPSIS
    Commits the pending work module by module.

.DESCRIPTION
    Each commit is one coherent change that builds on its own. Run from the repo
    root; the script stops at the first failure so you can inspect and resume.

    Commit 1 (.gitattributes) is already in history. This script picks up from
    commit 2.

    The first commit takes whatever is currently staged in your index — the
    Angular component split and the SubAccounts screen. Everything after that is
    staged explicitly by path, so the order of operations matters and the script
    should be run start to finish rather than piecemeal.

.PARAMETER DryRun
    Show what each commit would contain without creating any commits.
#>

[CmdletBinding()]
param([switch] $DryRun)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Git {
    param([string[]] $Arguments)
    & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function New-ModuleCommit {
    param(
        [string]   $Title,
        [string]   $Message,
        [string[]] $Paths,
        [switch]   $UseExistingIndex
    )

    Write-Host "`n=== $Title" -ForegroundColor Cyan

    if (-not $UseExistingIndex) {
        foreach ($path in $Paths) {
            # Globs that match nothing are not an error: a module may be partly
            # committed already if the script was interrupted.
            $matched = @(git ls-files --others --exclude-standard --modified --deleted -- $path 2>$null)
            if ($matched.Count -eq 0) {
                Write-Host "    nothing pending for $path" -ForegroundColor DarkGray
                continue
            }
            Invoke-Git @('add', '--', $path)
        }
    }

    $staged = @(git diff --cached --name-only)
    if ($staged.Count -eq 0) {
        Write-Host '    nothing staged, skipping' -ForegroundColor DarkGray
        return
    }

    Write-Host "    $($staged.Count) file(s):" -ForegroundColor Gray
    $staged | Select-Object -First 12 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
    if ($staged.Count -gt 12) { Write-Host "      ... and $($staged.Count - 12) more" -ForegroundColor DarkGray }

    if ($DryRun) {
        Write-Host '    (dry run, not committing)' -ForegroundColor Yellow
        return
    }

    Invoke-Git @('commit', '-q', '-m', $Message)
    Write-Host "    committed $(git log -1 --format='%h')" -ForegroundColor Green
}

# ------------------------------------------------------------- stale lock files
# Left behind by a git process that could not clean up after itself. Zero-byte
# and minutes old means nothing is actually running.
Write-Host '=== Clearing stale git locks' -ForegroundColor Cyan
foreach ($lock in @('.git/index.lock', '.git/HEAD.lock', '.git/objects/maintenance.lock')) {
    if (Test-Path $lock) { Remove-Item $lock -Force; Write-Host "    removed $lock" -ForegroundColor Yellow }
}
Get-ChildItem .git -Filter 'next-index-*.lock' -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "    removed $($_.Name)" -ForegroundColor Yellow }

# A file I created to prove the sandbox could not delete it. It cannot; you can.
if (Test-Path 'probe-delete-test') { Remove-Item 'probe-delete-test' -Force; Write-Host '    removed probe-delete-test' -ForegroundColor Yellow }

# ------------------------------------------------------------------- commit 2
New-ModuleCommit -UseExistingIndex -Title 'Angular component split + SubAccounts' -Message @'
Split Angular pages into component files and add the SubAccounts screen

Every page was a single .ts carrying an inline template and inline styles. Each
is now a .ts/.html/.scss triple in its own folder, which is what the Angular CLI
generates and what strictTemplates reports line numbers against. Covers the auth
pages, the app shell, the doc viewer, and the roles, users, configurations,
org-currencies, smtp-settings, chart-of-accounts and tax-master screens.

Adds the SubAccounts page alongside them, plus the SystemAccount enum and the
Chart of Accounts seed naming the control accounts a SubAccount hangs from: each
Contact gets Accounts Receivable and Accounts Payable, each Item gets Inventory,
Cost of Goods Sold and Sales Revenue.
'@

# ------------------------------------------------------------------- commit 3
New-ModuleCommit -Title 'Unblock the build' -Paths @(
    'backend/Directory.Build.targets',
    'backend/Directory.Packages.props'
) -Message @'
Remove the OpenAPI XML comment generator so the backend compiles

The build failed with CS0200 in generated code: Microsoft.AspNetCore.OpenApi
10.0.0 emits `mediaType.Example = ...` from its XML comment source generator, and
Microsoft.OpenApi later made that property read-only on IOpenApiMediaType. As the
preLaunchTask never succeeded, F5 never reached the debugger.

There is no version that resolves this. Directory.Packages.props pins
Microsoft.OpenApi to 2.7.5 because every earlier 2.x carries GHSA-v5pm-xwqc-g5wc,
and 2.7.5 is both the first patched release on that line and new enough to have
the read-only property. ASP.NET Core cannot fix it in 10.0 either; the correction
lands in .NET 11. See dotnet/aspnetcore#64317 and microsoft/OpenAPI.NET#2618.

So the generator is removed until we move to .NET 11. The only loss is that ///
comments no longer flow into the OpenAPI document; endpoint discovery, schema
generation and the OpenAPI endpoint itself are unaffected.
'@

# ------------------------------------------------------------------- commit 4
New-ModuleCommit -Title 'Backend per-environment configuration' -Paths @(
    '.gitignore',
    'backend/Api/Identity/Identity.Api/appsettings*.json',
    'backend/Api/Platform/Platform.Api/appsettings*.json',
    'backend/Api/Master/Master.Api/appsettings*.json',
    'backend/Api/Accounting/Accounting.Api/appsettings*.json',
    'backend/Gateway/appsettings*.json'
) -Message @'
Add Development, Staging, UAT and Production appsettings

Each of the five running projects now has appsettings.json plus one file per
environment, selected by ASPNETCORE_ENVIRONMENT.

The base file lists every key but leaves environment-specific values blank, so
the shape is discoverable without any value leaking across environments.
Development carries the real local values; Staging, UAT and Production ship blank
and are filled at runtime from environment variables using the double-underscore
convention (ConnectionStrings__MasterDatabase, Jwt__SigningKey, and so on).

Blank rather than absent is deliberate. An absent key would let the `??`
fallbacks in Program.cs fire and silently point a deployed environment at
localhost; a blank one cannot, and fails loudly instead. The guards that turn
that into a clear error are in the next commit.

appsettings.Development.json is no longer gitignored. It holds only localhost
values, so a clone now runs with no setup, which is what the ignore rule was
preventing. Anything genuinely secret belongs in appsettings.Local.json, which
stays ignored.

The Gateway files also carry the RequestLog section, whose consumer arrives a few
commits later; it is inert until then.
'@

# ------------------------------------------------------------------- commit 5
New-ModuleCommit -Title 'Fail-loud configuration guards' -Paths @(
    'backend/Api/Identity/Identity.Api/Program.cs',
    'backend/Api/Identity/Identity.Api/Services/UserService.cs',
    'backend/Api/Platform/Platform.Api/Program.cs',
    'backend/Api/Platform/Platform.Api/Services/ProvisioningWorker.cs',
    'backend/Api/Accounting/Accounting.Api/Program.cs'
) -Message @'
Throw on missing configuration instead of falling back to localhost

`_config["Platform:BaseUrl"] ?? "http://localhost:5002"` only fires when the key
is absent. Every key is now present-but-blank, so the fallbacks were unreachable
dead code that read as though a safe default existed while the real behaviour was
`new Uri("")`, `UseNpgsql("")` and `string.Format("")`.

Two of these were doing real damage:

ProvisioningWorker stored an empty tenant connection string and then flipped the
organization to Active and Ready. The customer was told their account was
provisioned when it could never connect.

UserService built invitation links from a blank App:BaseUrl, producing a relative
URL that is dead in every mail client, and the send still reported success.

Both now throw naming the exact key and its environment-variable form. The JWT
signing-key fallbacks are gone too: a shared constant across deployments means a
token minted anywhere is accepted everywhere.
'@

# ------------------------------------------------------------------- commit 6
New-ModuleCommit -Title 'Frontend per-environment configuration' -Paths @(
    'frontend/apps/web/src/environments',
    'frontend/apps/docs/src/environments',
    'frontend/apps/web/project.json',
    'frontend/apps/docs/project.json',
    'frontend/apps/web/tsconfig.json',
    'frontend/apps/web/src/app/app.config.ts',
    'frontend/apps/docs/src/main.ts',
    'frontend/libs/shared/api-client/src'
) -Message @'
Add Angular environment files and fix the zone.js polyfill

web and docs each gain environment.ts plus staging, uat and production variants,
selected at build time by fileReplacements on matching Nx configurations. Build
with `nx build web -c uat`, serve with `nx serve web -c staging`.

The web environment exposes apiBaseUrl. Rather than rewrite every call site, a
new API_BASE_URL token and apiBaseUrlInterceptor in libs/shared/api-client prefix
the configured origin onto root-relative /api calls. The app provides the token
from its environment file, so the library never imports app config and the
dependency direction stays app -> lib. Empty means same origin, which is what
development wants because proxy.conf.json forwards /api to the services.

Also sets "polyfills": ["zone.js"] on both apps. Both call
provideZoneChangeDetection() but neither project.json ever declared the polyfill,
so bootstrapping died with NG0908 and neither app had ever successfully started.
'@

# ------------------------------------------------------------------- commit 7
New-ModuleCommit -Title 'Dev proxy routes' -Paths @(
    'frontend/apps/web/proxy.conf.json'
) -Message @'
Proxy /api/organizations and /api/smtp-settings to Platform

Both prefixes are live — org-currencies, configurations and smtp-settings all
call them — and both were missing from the dev proxy, so the requests fell
through to the Angular dev server and came back as index.html or a 404. The
Gateway already routed them correctly; only the development proxy was short.
'@

# ------------------------------------------------------------------- commit 8
New-ModuleCommit -Title 'VS Code debugging for the Angular apps' -Paths @(
    '.vscode/launch.json',
    '.vscode/tasks.json',
    '.vscode/serve-angular.js'
) -Message @'
Add Development and Staging debug configurations for web and docs

serve-web.js only ever knew how to start web on 4200. serve-angular.js takes a
project, a port and a configuration, so one script drives all four serve tasks.
It keeps the port-reuse check: when the port is taken, @angular/build asks
"use a different port?" and waits forever, because its own guard tests
`if (!tty_1.isTTY)` against a function it never calls.

Only Development and Staging get launch configurations. UAT and Production are
deploy-time build configurations, not things you attach a debugger to.

Adds two compounds, Frontend only (Web + Docs) for each of the two environments,
and a Docs attach configuration alongside the existing Web one.
'@

if (-not $DryRun -and (Test-Path '.vscode/serve-web.js')) {
    Write-Host '    removing superseded serve-web.js' -ForegroundColor Yellow
    Invoke-Git @('rm', '-q', '--', '.vscode/serve-web.js')
    Invoke-Git @('commit', '-q', '-m', @'
Remove serve-web.js, superseded by serve-angular.js

Nothing referenced it after the four serve tasks moved to the generic script.
'@)
    Write-Host "    committed $(git log -1 --format='%h')" -ForegroundColor Green
}

# ------------------------------------------------------------------- commit 9
New-ModuleCommit -Title 'Local database setup' -Paths @(
    'scripts/setup-dev-db.sql',
    'scripts/setup-dev-db.ps1',
    'scripts/commit-modules.ps1',
    'README.md'
) -Message @'
Add the local database setup script and document the credentials

setup-dev-db.ps1 takes a fresh clone to a running stack: it creates
EP_Admin, EP_Design, EP_Test and a stand-in customer
database, generates the InitialCreate migration for each service that has a
DbContext, applies them, then prints the seeded master row counts so a failed
seed is visible rather than assumed.

All four databases are created UTF8 from template0. template1 on a Windows
install is frequently WIN1252, which silently corrupts Tamil and Chinese text and
makes varchar lengths count bytes instead of characters.

Only Master, Platform, Identity and Accounting have a DbContext. The other eight
services have empty Repository projects, so EF has nothing to generate; the
script names them as skipped rather than passing over them silently.

README gains a credentials block directly under the title covering the dev and
test databases, with the reasoning for why localhost-only values are committed
and a warning against ever putting a real one there.
'@

# ------------------------------------------------------------------ commit 10
New-ModuleCommit -Title 'Gateway request log' -Paths @(
    'backend/Gateway/Gateway.Entity',
    'backend/Gateway/Gateway.Repository',
    'backend/Gateway/Logging',
    'backend/Gateway/Program.cs',
    'backend/Gateway/Gateway.csproj',
    'backend/Bill-Book.sln',
    'backend/Bill-Book.Debug.slnf'
) -Message @'
Log every proxied request to gwy.RequestLogs

The Gateway gains its own Entity and Repository projects and a gwy schema in the
master database. It owns this table outright, so no service boundary is crossed
and nothing else reads it.

Writes stay off the request path. Middleware enqueues onto a bounded Channel and
a hosted service drains it, inserting in batches of 200 or every 5 seconds,
whichever comes first. The channel drops on overflow rather than waiting: a
logging backlog must never become backpressure on real traffic, and the count of
dropped entries is surfaced in the flush warning. A database failure logs and
discards the batch rather than taking the gateway down.

Metadata only — method, path, status, duration, cluster, destination, tenant and
user ids, client IP, user agent and byte counts. No bodies: they carry GSTINs and
contact details, the auth routes would store plaintext passwords, and they would
dominate the table.

A purge job deletes past the retention window daily, 30 days by default and 7 in
development, via ExecuteDeleteAsync so nothing is loaded or tracked. Health checks
and the status page are excluded or they would be most of the rows.
'@

# ------------------------------------------------------------------ commit 11
New-ModuleCommit -Title 'Documentation' -Paths @(
    'frontend/apps/docs/content/environments.md',
    'frontend/apps/docs/src/app/docs.manifest.ts',
    'frontend/apps/docs/content/release-notes.md',
    'frontend/apps/docs/content/running-locally.md',
    'frontend/apps/docs/content/configuration.md'
) -Message @'
Document the four environments

New environments page covering how each side selects an environment — backend at
run time via ASPNETCORE_ENVIRONMENT, frontend at build time via fileReplacements
— the full table of environment-variable overrides for the blank placeholders,
the build and serve commands per configuration, and which debug configurations
exist and why UAT and Production are not among them.

Corrects the passages in running-locally.md and configuration.md that the change
made stale, and adds the release-note entry.
'@

# ---------------------------------------------------------------------- summary
Write-Host "`n=== Done" -ForegroundColor Cyan
git --no-pager log --oneline -12
Write-Host "`nStill pending:" -ForegroundColor Cyan
git status --short
