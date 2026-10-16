#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
dotnet run --project tests/Library.Tests
