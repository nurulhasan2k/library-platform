#!/usr/bin/env bash
# Starts the Library Platform on http://localhost:5080 (creates and fills the demo data on the first run).
set -e
cd "$(dirname "$0")/.."
command -v dotnet >/dev/null || { echo "The .NET 10 SDK is not installed. See docs/INSTALLATION.md."; exit 1; }
exec dotnet run --project src/Library.Web
