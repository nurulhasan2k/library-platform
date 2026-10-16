#!/usr/bin/env bash
# Tries the public API. Start the site first (scripts/run.sh). Usage: scripts/api-examples.sh [base-url] [api-key]
BASE="${1:-http://localhost:5080}"
KEY="${2:-demo-api-key-2026}"
show() { echo; echo "\$ $1"; shift; "$@" | head -c 700; echo; }
show "status" curl -s -i -H "X-Api-Key: $KEY" "$BASE/api/v1/status"
show "list items" curl -s -H "X-Api-Key: $KEY" "$BASE/api/v1/items?pageSize=3"
show "search: books that are available" curl -s -H "X-Api-Key: $KEY" "$BASE/api/v1/items?q=dune&type=book&availableOnly=true"
show "one item by library code" curl -s -H "X-Api-Key: $KEY" "$BASE/api/v1/items/BK-001"
show "categories" curl -s -H "X-Api-Key: $KEY" "$BASE/api/v1/categories"
show "branches with opening hours" curl -s -H "X-Api-Key: $KEY" "$BASE/api/v1/branches"
show "missing key -> 401" curl -s -i "$BASE/api/v1/items"
show "wrong key -> 403" curl -s -i -H "X-Api-Key: nope" "$BASE/api/v1/items"
show "write attempt -> 405" curl -s -i -X POST -H "X-Api-Key: $KEY" "$BASE/api/v1/items"
show "unknown item -> 404" curl -s -i -H "X-Api-Key: $KEY" "$BASE/api/v1/items/NOPE-1"
