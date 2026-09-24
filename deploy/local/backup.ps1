# Backs up the database and uploaded files to deploy/local/backups.
#
#   powershell -ExecutionPolicy Bypass -File backup.ps1            back up now
#   powershell -ExecutionPolicy Bypass -File backup.ps1 -Install   and every night at 2 AM
#
# A backup on the same disk survives a mistake, not a dead disk or a stolen PC.
# Copy the backups folder somewhere else as well - OneDrive, Google Drive or a
# USB drive - or it is not really a backup.

param(
    [switch] $Install,
    [string] $At = '02:00',
    [int] $KeepDays = 14
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Install) {
    $script = Join-Path $PSScriptRoot 'backup.ps1'
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -KeepDays $KeepDays"
    $trigger = New-ScheduledTaskTrigger -Daily -At $At
    # Catches up after the PC was off or asleep at 2 AM, rather than skipping.
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

    Register-ScheduledTask -TaskName 'BillBook backup' -Action $action -Trigger $trigger `
        -Settings $settings -Description 'Backs up the Bill Book database and files.' -Force | Out-Null

    Write-Host "Scheduled: every day at $At, keeping $KeepDays days of backups." -ForegroundColor Green
    Write-Host "It runs while you are signed in, the same as Docker Desktop."
    exit 0
}

docker compose exec -T -e KEEP_DAYS=$KeepDays db sh /scripts/backup.sh
if ($LASTEXITCODE -ne 0) {
    throw "Backup failed (exit code $LASTEXITCODE). Is the stack running? Try: docker compose ps"
}
