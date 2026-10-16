# Runs the automated tests (unit/integration tests; they use their own temporary data and never touch data/library.json).
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
dotnet run --project tests/Library.Tests
