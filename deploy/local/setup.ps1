# Writes the settings for running Bill-Book on your own PCs.
#
#   powershell -ExecutionPolicy Bypass -File setup.ps1
#
# One PC:   writes .env beside this script.
# Split:    writes pcs\<part>.env, one per PC. Copy each to that PC as
#           deploy\local\.env. Each file holds only what its PC needs - the web
#           PC is never given the database password, for instance.
#
# Run once. It refuses to overwrite earlier output, because the secrets in it
# are not replaceable: a new database password no longer opens the existing
# database, a new JWT key signs everyone out, and a new encryption key makes
# every stored SMTP password unreadable.
#
# Written for Windows PowerShell 5.1, which is what Windows 11 ships, so it
# needs nothing installed.

$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'
$pcsDir  = Join-Path $PSScriptRoot 'pcs'

if ((Test-Path $envFile) -or (Test-Path $pcsDir)) {
    Write-Host ".env or pcs\ already exists; leaving them alone. Edit them directly to change a setting." -ForegroundColor Yellow
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

# Free text is written in single quotes: Docker Compose reads a bare '#' as the
# start of a comment and a '$' as a variable. A single quote cannot be escaped
# inside single quotes, so an apostrophe is written as the typographic one,
# which looks the same on an invoice.
function Format-EnvText([string] $value) {
    return "'" + ($value -replace "'", [string][char]0x2019) + "'"
}

function Read-Required([string] $prompt) {
    $value = ''
    while (-not $value) { $value = (Read-Host $prompt).Trim() }
    return $value
}

# An IPv4 address. Names are not accepted: a container cannot resolve another
# Windows PC's name, only an address - give each PC a fixed one on the router.
function Read-Address([string] $prompt) {
    $value = ''
    while ($value -notmatch '^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$') {
        $value = (Read-Host "$prompt (e.g. 192.168.1.20)").Trim()
    }
    return $value
}

function Write-EnvFile([string] $path, [string[]] $lines) {
    # UTF-8 without a byte-order mark. Windows PowerShell's own -Encoding UTF8
    # writes one, and Docker Compose would read it as part of the first name.
    [System.IO.File]::WriteAllText($path, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------------------
# Questions
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Where will Bill-Book run?"
Write-Host "  1. Everything on this PC"
Write-Host "  2. Split across PCs: database, services, worker, gateway and web apps each on their own"
$layout = ''
while ($layout -notin @('1', '2')) { $layout = (Read-Host "Choose 1 or 2").Trim() }
$split = $layout -eq '2'

Write-Host ""
Write-Host "Your domain name, if you have one (for example: mybillbook.in)."
Write-Host "Leave it empty to run without one for now; you can add it later."
$domain = (Read-Host "Domain").Trim().ToLowerInvariant() -replace '^https?://', '' -replace '/.*$', ''

Write-Host ""
Write-Host "Your business name, as it should appear on invoices (you can change it later)."
$company = Read-Required "Business name"

Write-Host ""
Write-Host "Your name and email address. This becomes the owner account you sign in with."
$ownerName = Read-Required "Your name"
$ownerEmail = ''
while ($ownerEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { $ownerEmail = (Read-Host "Your email").Trim() }

if ($split) {
    Write-Host ""
    Write-Host "The fixed network address of each PC that others connect to."
    Write-Host "(Reserve these on your router so they never change.)"
    $dbHost       = Read-Address "Database PC"
    $servicesHost = Read-Address "Services PC"
    $gatewayHost  = Read-Address "Gateway PC"
}

Write-Host ""
Write-Host "Uploaded files and archived invoices can be kept on an SFTP server on another PC"
Write-Host "(README.md, 'The file server PC', shows how to set one up on Windows)."
$useSftp = (Read-Host "Use an SFTP server? (y/N)").Trim().ToLowerInvariant() -eq 'y'
if ($useSftp) {
    $sftpHost = Read-Address "SFTP server PC"
    $sftpUser = Read-Required "SFTP user name"
    $secure   = Read-Host "SFTP password" -AsSecureString
    # PtrToStringBSTR, not PtrToStringAuto: a BSTR is UTF-16 everywhere, and
    # Auto reads it as UTF-8 outside Windows - keeping only the first letter.
    $bstr     = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $sftpPass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    Write-Host "The server's key fingerprint, from 'ssh-keygen -lf' on that PC (SHA256:...)."
    Write-Host "Strongly recommended: it stops another machine posing as your file server."
    $sftpKey  = (Read-Host "Fingerprint (Enter to skip)").Trim()
}

# ---------------------------------------------------------------------------
# Values
# ---------------------------------------------------------------------------

$postgresPassword = New-Password 32
$jwtKey           = [Convert]::ToBase64String((New-RandomBytes 48))
$internalKey      = -join ((New-RandomBytes 32) | ForEach-Object { $_.ToString('x2') })
# Exactly 32 bytes: the service refuses any other length.
$encryptionKey    = [Convert]::ToBase64String((New-RandomBytes 32))
# Generated rather than typed, so it is never a weak one. Shown once below and
# kept in the services settings; change it in the app after the first sign-in.
$ownerPassword    = New-Password 16

if ($domain) {
    $appUrl    = "https://app.$domain"
    $portalUrl = "https://portal.$domain"
} else {
    $appUrl    = 'http://localhost:8081'
    $portalUrl = 'http://localhost:8082'
}

$header = @(
    '# Written by setup.ps1. KEEP A COPY SOMEWHERE SAFE: without the secrets in',
    '# it the database cannot be opened and stored SMTP passwords cannot be read.',
    '# Never commit it; deploy/local/.gitignore already excludes it.',
    ''
)

if ($split) {
    $database = @(
        '# The database PC. Its port is 5433.',
        "DB_HOST=$dbHost",
        'DB_PORT=5433',
        "POSTGRES_PASSWORD=$postgresPassword",
        ''
    )
    $servicesAt = @("SERVICES_HOST=$servicesHost", '')
} else {
    $database   = @("POSTGRES_PASSWORD=$postgresPassword", '')
    $servicesAt = @()
}

$keys = @(
    "JWT_SIGNING_KEY=$jwtKey",
    "INTERNAL_API_KEY=$internalKey",
    "ENCRYPTION_KEY=$encryptionKey",
    ''
)

$owner = @(
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
    ''
)

if ($useSftp) {
    $files = @(
        '# Uploaded files, on the SFTP server PC.',
        "SFTP_HOST=$sftpHost",
        "SFTP_USERNAME=$(Format-EnvText $sftpUser)",
        "SFTP_PASSWORD=$(Format-EnvText $sftpPass)",
        '# The folder on the server; / is the folder the account is locked into.',
        'SFTP_ROOT=/',
        "SFTP_HOST_KEY=$sftpKey",
        ''
    )
} else {
    $files = @(
        '# Uploaded files: kept on this PC. Set SFTP_HOST, SFTP_USERNAME,',
        '# SFTP_PASSWORD and SFTP_HOST_KEY to move them to an SFTP server.',
        ''
    )
}

$tunnel = @(
    '# Cloudflare Tunnel (README.md). Paste the token, then add ",tunnel" to',
    '# COMPOSE_PROFILES so `docker compose up -d` starts the tunnel too.',
    'CLOUDFLARE_TUNNEL_TOKEN=',
    ''
)

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

if (-not $split) {
    Write-EnvFile $envFile ($header + @(
        'COMPOSE_PROFILES=db,services,worker,gateway,web',
        '# 127.0.0.1 keeps every port to this PC (plus the tunnel). 0.0.0.0 also',
        '# lets other PCs on the office network open http://<this-pc>:8081.',
        'BIND_ADDRESS=127.0.0.1',
        '') + $database + $keys + $owner + $files + $tunnel)

    Write-Host ""
    Write-Host "Wrote $envFile" -ForegroundColor Green
    Write-Host "Copy it somewhere safe (a USB drive, a password manager). Then:"
    Write-Host ""
    Write-Host "    docker compose up -d --build"
} else {
    New-Item -ItemType Directory -Path $pcsDir | Out-Null

    # Opened to the network: the other PCs connect to these.
    $open = @('BIND_ADDRESS=0.0.0.0', '')

    Write-EnvFile (Join-Path $pcsDir 'database.env') ($header + @('COMPOSE_PROFILES=db') + $open + @(
        "POSTGRES_PASSWORD=$postgresPassword", ''))

    Write-EnvFile (Join-Path $pcsDir 'services.env') ($header + @('COMPOSE_PROFILES=services') + $open +
        $database + $keys + $owner + $files)

    Write-EnvFile (Join-Path $pcsDir 'worker.env') ($header + @('COMPOSE_PROFILES=worker', '') +
        $database + $servicesAt + $keys)

    Write-EnvFile (Join-Path $pcsDir 'gateway.env') ($header + @('COMPOSE_PROFILES=gateway') + $open +
        $database + $servicesAt + $keys)

    Write-EnvFile (Join-Path $pcsDir 'web.env') ($header + @(
        'COMPOSE_PROFILES=web',
        '# 127.0.0.1 keeps the apps to this PC and the tunnel; 0.0.0.0 also lets',
        '# other PCs on the office network open http://<this-pc>:8081.',
        'BIND_ADDRESS=127.0.0.1',
        '',
        "GATEWAY_HOST=$gatewayHost",
        '') + $tunnel)

    Write-Host ""
    Write-Host "Wrote one settings file per PC in $pcsDir" -ForegroundColor Green
    Write-Host "Copy each to its PC as deploy\local\.env (README.md, 'Split across PCs'):"
    Write-Host "    database.env  services.env  worker.env  gateway.env  web.env"
    Write-Host "Then keep the pcs folder somewhere safe and delete it from this PC."
}

Write-Host ""
Write-Host "Sign in with:" -ForegroundColor Green
Write-Host "    Email:    $ownerEmail"
Write-Host "    Password: $ownerPassword"
Write-Host ""
