<#
.SYNOPSIS
  Builds a .unitypackage from the tracked files of a UPM package repository, without launching Unity.

.DESCRIPTION
  A .unitypackage is a gzipped tar with one folder per asset, named by the asset's GUID, holding
  "asset" (the file; absent for folders), "asset.meta" and "pathname". The pathname is
  "Packages/<package name>/<relative path>", so importing the package embeds it under Packages/.
  Files without a .meta (hidden files, Tools~, .github) are skipped, like Unity would.

.PARAMETER PackageRoot
  Repository root that is the package (contains package.json). Default: the parent of this script.

.PARAMETER OutputDir
  Where to write <name>-<version>.unitypackage. Default: <workspace>/dist, the workspace being the
  parent of the package repository.

.EXAMPLE
  pwsh Tools~/Export-UnityPackage.ps1
  pwsh ../shiori/Tools~/Export-UnityPackage.ps1 -PackageRoot .
#>
param(
    [string]$PackageRoot = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

if ($PackageRoot -eq "") { $PackageRoot = Join-Path $PSScriptRoot ".." }
$PackageRoot = (Resolve-Path $PackageRoot).Path
$manifestPath = Join-Path $PackageRoot "package.json"
if (-not (Test-Path $manifestPath)) { throw "package.json not found in $PackageRoot" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$name = $manifest.name
$version = $manifest.version
if (-not $name -or -not $version) { throw "package.json needs name and version" }

if ($OutputDir -eq "") { $OutputDir = Join-Path (Split-Path $PackageRoot -Parent) "dist" }
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$output = Join-Path $OutputDir "$name-$version.unitypackage"

# Prefer the bsdtar Windows ships: a GNU tar from Git for Windows on PATH reads "C:\..." as a remote host.
$tar = Join-Path $env:SystemRoot "System32\tar.exe"
if (-not (Test-Path $tar)) {
    $found = Get-Command tar -ErrorAction SilentlyContinue
    if (-not $found) { throw "tar was not found (Windows 10+ ships one in System32)" }
    $tar = $found.Source
}

$tracked = & git -C $PackageRoot ls-files
if ($LASTEXITCODE -ne 0) { throw "git ls-files failed in $PackageRoot" }
$trackedSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$tracked, [StringComparer]::Ordinal)

$staging = Join-Path ([IO.Path]::GetTempPath()) ("unitypackage-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $staging | Out-Null
$entries = [System.Collections.Generic.List[string]]::new()
$utf8 = [Text.UTF8Encoding]::new($false)

try {
    foreach ($metaRelative in ($tracked | Where-Object { $_ -like "*.meta" } | Sort-Object)) {
        $assetRelative = $metaRelative.Substring(0, $metaRelative.Length - 5)
        # Unity ignores folders ending in ~ and hidden files; their metas are never tracked, but be safe.
        if ($assetRelative -match '(^|/)[^/]*~(/|$)' -or $assetRelative -match '(^|/)\.[^/]*$') { continue }

        $metaFull = Join-Path $PackageRoot $metaRelative
        $assetFull = Join-Path $PackageRoot $assetRelative
        $isFolder = Test-Path $assetFull -PathType Container
        if (-not $isFolder -and -not $trackedSet.Contains($assetRelative)) { continue }

        $guidLine = Select-String -Path $metaFull -Pattern '^guid:\s*([0-9a-fA-F]{32})' | Select-Object -First 1
        if (-not $guidLine) { throw "no guid in $metaRelative" }
        $guid = $guidLine.Matches[0].Groups[1].Value.ToLowerInvariant()

        $dir = Join-Path $staging $guid
        if (Test-Path $dir) { throw "duplicate guid $guid ($assetRelative)" }
        New-Item -ItemType Directory $dir | Out-Null
        Copy-Item $metaFull (Join-Path $dir "asset.meta")
        if (-not $isFolder) { Copy-Item $assetFull (Join-Path $dir "asset") }
        [IO.File]::WriteAllText((Join-Path $dir "pathname"), "Packages/$name/$assetRelative", $utf8)
        $entries.Add($guid)
    }

    if ($entries.Count -eq 0) { throw "nothing to export: no tracked .meta files in $PackageRoot" }

    # Entry names are written exactly as listed (no leading ./), the way Unity writes them.
    $list = Join-Path $staging "entries.txt"
    [IO.File]::WriteAllText($list, ($entries -join "`n") + "`n", $utf8)
    if (Test-Path $output) { Remove-Item $output }
    & $tar -czf $output -C $staging -T $list
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
}

Write-Host "Exported $($entries.Count) assets: $output"
