# Writes deploy/local/.env: the secrets, and the site's domain if it has one.
#
#   powershell -ExecutionPolicy Bypass -File setup.ps1
#
# Run once. It refuses to overwrite an existing .env, because the secrets in it
# are not replaceable: a new database password no longer opens the existing
# database, a new JWT key signs everyone out, and a new encryption key makes
# every stored SMTP password unreadable.
#
# Written for Windows PowerShell 5.1, which is what Windows 11 ships, so it
# needs nothing installed.

$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'

if (Test-Path $envFile) {
    Write-Host ".env already exists; leaving it alone. Edit it directly to change a setting." -ForegroundColor Yellow
    exit 0
}

# Cryptographic randomness. Get-Random is not; this is the .NET generator, in the
# form Windows PowerShell 5.1 has (the static GetBytes(int) is newer).
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-RandomBytes([int] $count) {
    $bytes = New-Object byte[] $count
    $rng.GetBytes($bytes)
    return $bytes
}

# Letters and digits only: it goes inside a connection string, where a ';' or a
# quote would end it early.
function New-Password([int] $length) {
    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-RandomBytes $length
    return -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

$postgresPassword = New-Password 32
$jwtKey           = [Convert]::ToBase64String((New-RandomBytes 48))
$internalKey      = -join ((New-RandomBytes 32) | ForEach-Object { $_.ToString('x2') })
# Exactly 32 bytes: the service refuses any other length.
$encryptionKey    = [Convert]::ToBase64String((New-RandomBytes 32))

Write-Host ""
Write-Host "Your domain name, if you have one (for example: mybillbook.in)."
Write-Host "Leave it empty to run on this PC only; you can add it to .env later."
$domain = (Read-Host "Domain").Trim().ToLowerInvariant() -replace '^https?://', '' -replace '/.*$', ''

Write-Host ""
Write-Host "Your business name, as it should appear on invoices (you can change it later)."
$company = ''
while (-not $company) { $company = (Read-Host "Business name").Trim() }

Write-Host ""
Write-Host "Your name and email address. This becomes the owner account you sign in with."
$ownerName = ''
while (-not $ownerName) { $ownerName = (Read-Host "Your name").Trim() }
$ownerEmail = ''
while ($ownerEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { $ownerEmail = (Read-Host "Your email").Trim() }

# Names are free text, so they are written in single quotes: Docker Compose
# reads a bare '#' as the start of a comment and a '$' as a variable. A single
# quote cannot be escaped inside single quotes, so an apostrophe is written as
# the typographic one, which looks the same on an invoice.
function Format-EnvText([string] $value) {
    return "'" + ($value -replace "'", [string][char]0x2019) + "'"
}

# Generated rather than typed, so it is never a weak one. Shown once below and
# kept in .env; change it in the app after the first sign-in if you prefer.
$ownerPassword = New-Password 16

if ($domain) {
    $appUrl    = "https://app.$domain"
    $portalUrl = "https://portal.$domain"
} else {
    $appUrl    = 'http://localhost:8081'
    $portalUrl = 'http://localhost:8082'
}

$lines = @(
    '# Written by setup.ps1. KEEP A COPY OF THIS FILE SOMEWHERE SAFE: without it',
    '# the database cannot be opened and stored SMTP passwords cannot be read.',
    '# Never commit it; deploy/local/.gitignore already excludes it.',
    '',
    "POSTGRES_PASSWORD=$postgresPassword",
    "JWT_SIGNING_KEY=$jwtKey",
    "INTERNAL_API_KEY=$internalKey",
    "ENCRYPTION_KEY=$encryptionKey",
    '',
    '# Where links in emails point. The app itself works on any address.',
    "APP_URL=$appUrl",
    "PORTAL_URL=$portalUrl",
    '',
    '# The owner account, your business and its first branch. Created on the',
    '# first start only, while the database has no users; changing these later',
    '# changes nothing.',
    "BOOTSTRAP_COMPANY_NAME=$(Format-EnvText $company)",
    "BOOTSTRAP_OWNER_NAME=$(Format-EnvText $ownerName)",
    "BOOTSTRAP_OWNER_EMAIL=$ownerEmail",
    "BOOTSTRAP_OWNER_PASSWORD=$ownerPassword",
    '',
    '# Cloudflare Tunnel (README.md, step 4). Paste the token, then uncomment the',
    '# COMPOSE_PROFILES line so `docker compose up -d` starts the tunnel too.',
    'CLOUDFLARE_TUNNEL_TOKEN=',
    '# COMPOSE_PROFILES=tunnel',
    '',
    '# 127.0.0.1 keeps the apps to this PC (plus the tunnel). 0.0.0.0 also lets',
    '# other PCs on the office network open http://<this-pc>:8081.',
    'BIND_ADDRESS=127.0.0.1'
)

# UTF-8 without a byte-order mark. Windows PowerShell's own -Encoding UTF8 writes
# one, and Docker Compose would read it as part of the first variable's name.
[System.IO.File]::WriteAllText($envFile, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
Write-Host "Wrote $envFile" -ForegroundColor Green
Write-Host ""
Write-Host "Sign in with:" -ForegroundColor Green
Write-Host "    Email:    $ownerEmail"
Write-Host "    Password: $ownerPassword"
Write-Host ""
Write-Host "Copy .env somewhere safe (a USB drive, a password manager). Then:"
Write-Host ""
Write-Host "    docker compose up -d --build"
Write-Host ""
