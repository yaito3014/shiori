<#
.SYNOPSIS
  Installs the git post-commit hook that exports <workspace>/dist/<name>-<version>.unitypackage
  after every commit, into this repository or another package repository next to it.

.PARAMETER PackageRoot
  The package repository to install into. Default: the parent of this script (the core).
  For shiori-vrchat: pwsh ../shiori/Tools~/Install-Hooks.ps1 -PackageRoot .
#>
param(
    [string]$PackageRoot = ""
)

$ErrorActionPreference = "Stop"

if ($PackageRoot -eq "") { $PackageRoot = Join-Path $PSScriptRoot ".." }
$PackageRoot = (Resolve-Path $PackageRoot).Path
$gitDir = & git -C $PackageRoot rev-parse --git-dir
if ($LASTEXITCODE -ne 0) { throw "not a git repository: $PackageRoot" }
if (-not [IO.Path]::IsPathRooted($gitDir)) { $gitDir = Join-Path $PackageRoot $gitDir }
$hooksDir = Join-Path $gitDir "hooks"
New-Item -ItemType Directory -Force $hooksDir | Out-Null

# The hook runs under git's sh; the exporter is addressed relative to the repository root so the
# hook keeps working when the workspace moves. For a sibling package it points back into shiori.
$exporter = [IO.Path]::GetRelativePath($PackageRoot, (Join-Path $PSScriptRoot "Export-UnityPackage.ps1")) -replace "\\", "/"

$hook = @"
#!/bin/sh
# Installed by shiori/Tools~/Install-Hooks.ps1: export <workspace>/dist/<name>-<version>.unitypackage after each commit.
root="`$(git rev-parse --show-toplevel)"
pwsh -NoProfile -File "`$root/$exporter" -PackageRoot "`$root" || echo "post-commit: unitypackage export failed" >&2
"@

$hookPath = Join-Path $hooksDir "post-commit"
[IO.File]::WriteAllText($hookPath, $hook.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
Write-Host "Installed $hookPath"
Write-Host "Exporter: $exporter"
