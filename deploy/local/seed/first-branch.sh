#!/bin/sh
# Loads the first branch's master data: chart of accounts, tax rates, units,
# numbering series, report list, print templates and contact roles. Runs after
# every `docker compose up`, and changes nothing on a branch that already has
# them, so a new version's added seed rows reach the branch on the next start.
#
# Why this exists. Master creates the first owner, customer and branch while
# migrating (Bootstrap:OwnerEmail), but migration runs before any other service
# is up, so nothing can seed the branch at that moment. A branch created from
# the app is seeded at creation; this does the same for the first one, through
# the same internal endpoint each service exposes for exactly that.
#
# The ids are the ones Master gives the bootstrap customer and its branch.
set -eu

if [ -z "${BOOTSTRAP_OWNER_EMAIL:-}" ]; then
  echo "BOOTSTRAP_OWNER_EMAIL is empty: there is no first branch to seed."
  exit 0
fi

id=00000000-0000-0000-0000-000000000001
body="{\"customerId\":\"$id\",\"orgId\":\"$id\",\"vertical\":\"General\"}"

# The order Master's own seeder uses. Each service is given two minutes to come
# up and finish its start-up migration check.
# Each service's own port, as docker-compose.yml assigns them.
for target in accounting:7502 inventory:7503 sales:7504 purchase:7505 reporting:7507 printing:7508 master:7501; do
  svc="${target%%:*}"
  curl --fail --silent --show-error --output /dev/null \
    --retry 60 --retry-delay 2 --retry-all-errors \
    -X POST "http://$target/internal/seed/organization" \
    -H "Content-Type: application/json" \
    -H "X-Internal-Key: $INTERNAL_API_KEY" \
    -d "$body"
  echo "seeded $svc"
done

echo "The first branch is ready."
