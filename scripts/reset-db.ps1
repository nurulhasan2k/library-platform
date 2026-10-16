# Deletes ALL data and creates the demo data again, then exits. Stop the running site first.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
dotnet run --project src/Library.Web -- --reset-db --exit
Write-Host "Done. Start the site again with scripts\run.ps1"
