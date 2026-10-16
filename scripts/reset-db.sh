#!/usr/bin/env bash
# Deletes ALL data and creates the demo data again, then exits. Stop the running site first.
set -e
cd "$(dirname "$0")/.."
dotnet run --project src/Library.Web -- --reset-db --exit
echo "Done. Start the site again with scripts/run.sh"
