#!/bin/sh
# Runs inside the db container (backup.ps1 calls it). Dumps every database and
# the uploaded files into /backups — deploy/local/backups on the PC — and keeps
# the last KEEP_DAYS days.
#
# Every database rather than a fixed list: the first shard is IN000001, and a
# second one appears the day the first fills up. A backup that named only the
# first would stop covering new customers without saying so.
set -eu

KEEP_DAYS="${KEEP_DAYS:-14}"
stamp="$(date +%Y-%m-%d_%H%M)"
dir="/backups/$stamp"
mkdir -p "$dir"

databases="$(psql -U postgres -At -c \
  "SELECT datname FROM pg_database WHERE datistemplate = false AND datname <> 'postgres' ORDER BY datname")"

for db in $databases; do
  # Custom format: compressed, and pg_restore can put back one table from it.
  pg_dump -U postgres -Fc -f "$dir/$db.dump" "$db"
  echo "  $db -> $stamp/$db.dump"
done

# Roles and their passwords live outside every database.
pg_dumpall -U postgres --globals-only > "$dir/globals.sql"

tar -czf "$dir/files.tar.gz" -C /data files
echo "  uploaded files -> $stamp/files.tar.gz"

# Older folders only, never the one just written, even if the clock is wrong.
find /backups -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" ! -name "$stamp" -exec rm -rf {} +

echo "Backup complete: backups/$stamp"
