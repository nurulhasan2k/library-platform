# Starts the Library Platform on http://localhost:5080 (creates and fills the demo data on the first run).
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Error "The .NET 10 SDK is not installed. See docs/INSTALLATION.md."; exit 1 }
dotnet run --project src/Library.Web
