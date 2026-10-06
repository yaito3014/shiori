<#
.SYNOPSIS
  Copies a dev project to a folder outside this repository so the setup wizard can
  run "git init" there. Inside the repository the wizard refuses (nested repository).

.EXAMPLE
  pwsh DevProject~/New-Sandbox.ps1 -Version 2022.3
  pwsh DevProject~/New-Sandbox.ps1 -Version 6000.6 -Destination D:\tmp\shiori-sandbox
#>
param(
    [string]$Version = "2022.3",
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"

$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $PSScriptRoot $Version
if (-not (Test-Path $source)) { throw "No dev project for Unity $Version under $PSScriptRoot" }

if ($Destination -eq "") {
    $Destination = Join-Path (Split-Path $repo -Parent) "shiori-sandbox\$Version"
}
if (Test-Path $Destination) { throw "Destination already exists: $Destination (delete it first)" }

New-Item -ItemType Directory -Force $Destination | Out-Null
foreach ($folder in @("Assets", "Packages", "ProjectSettings")) {
    Copy-Item (Join-Path $source $folder) $Destination -Recurse
}

$manifest = Join-Path $Destination "Packages\manifest.json"
$packagePath = "file:" + ($repo -replace "\\", "/")
(Get-Content $manifest -Raw) -replace "file:\.\./\.\./\.\.", $packagePath | Set-Content $manifest -NoNewline

Write-Host "Sandbox created: $Destination"
Write-Host "Package reference: $packagePath"
Write-Host "Open it with Unity $Version via Unity Hub (Add project from disk)."
