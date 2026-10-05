#!/bin/sh
# Loads the first branch's master data: chart of accounts, tax rates, units,
# numbering series, report list, print templates and contact roles. Runs after
# every `docker compose up`, and changes nothing on a branch that already has
# them, so a new version's added seed rows reach the branch on the next start.
#
# Why this exists. Master creates the first owner, customer and branch while
# migrating (Bootstrap:OwnerEmail), but migration runs before any other service
# is up, so nothing can seed the branch at that moment. A branch created from
# the app is seeded at creation; this asks Master to do the same for the first
# one.
#
# Master does the fan-out itself (TK-153): it reads the branch's real business
# type and add-ons — the first customer is the Internal one, a Service business
# — and seeds exactly the services those call for. It answers 503 until every
# one of them is up and seeded, so this retries for up to two minutes.
#
# The ids are the ones Master gives the bootstrap customer and its branch.
set -eu

if [ -z "${BOOTSTRAP_OWNER_EMAIL:-}" ]; then
  echo "BOOTSTRAP_OWNER_EMAIL is empty: there is no first branch to seed."
  exit 0
fi

id=00000000-0000-0000-0000-000000000001
body="{\"customerId\":\"$id\",\"orgId\":\"$id\"}"

curl --fail --silent --show-error --output /dev/null \
  --retry 60 --retry-delay 2 --retry-all-errors \
  -X POST "http://master:7501/internal/seed/branch" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_API_KEY" \
  -d "$body"

echo "The first branch is ready."
