<#
.SYNOPSIS
  Creates a minimal Unity project that uses this package, outside the repository by default
  so the setup wizard can run "git init" in it.

.PARAMETER UnityVersion
  Full editor version, e.g. 2022.3.22f1 or 6000.6.0f1.

.PARAMETER Destination
  Where to create the project. Default: <repo parent>/shiori-dev/<major.minor>.

.PARAMETER Absolute
  Reference the package by absolute path instead of a path relative to the project.

.PARAMETER Embed
  Copy the tracked package files into <project>/Packages/com.yaito3014.shiori instead of
  referencing the repository with "file:". Required when the project lives inside the
  repository (CI): the Package Manager fingerprints a file: package's whole folder, and a
  project changing inside it makes Unity re-resolve packages forever.

.EXAMPLE
  pwsh Tools~/New-DevProject.ps1 -UnityVersion 2022.3.22f1
  pwsh Tools~/New-DevProject.ps1 -UnityVersion 6000.6.0f1 -Destination ci-project~/6000.6.0f1 -Embed
#>
param(
    [Parameter(Mandatory = $true)] [string]$UnityVersion,
    [string]$Destination = "",
    [switch]$Absolute,
    [switch]$Embed
)

$ErrorActionPreference = "Stop"

if ($UnityVersion -notmatch '^(\d+)\.(\d+)\.\d+[abfp]\d+$') {
    throw "UnityVersion must look like 2022.3.22f1 (got '$UnityVersion')"
}
$stream = "$($Matches[1]).$($Matches[2])"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ($Destination -eq "") {
    $Destination = Join-Path (Join-Path (Split-Path $repo -Parent) "shiori-dev") $stream
}
if (Test-Path $Destination) { throw "Destination already exists: $Destination" }

New-Item -ItemType Directory -Force (Join-Path $Destination "Assets") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $Destination "Packages") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $Destination "ProjectSettings") | Out-Null
$Destination = (Resolve-Path $Destination).Path
$packagesDir = Join-Path $Destination "Packages"

if ($Embed) {
    $target = Join-Path $packagesDir "com.yaito3014.shiori"
    New-Item -ItemType Directory -Force $target | Out-Null
    # Tracked files only: no Library, no ci-project~, no .git.
    $files = & git -C $repo ls-files
    if ($LASTEXITCODE -ne 0 -or -not $files) { throw "git ls-files failed; the embed option needs a git checkout" }
    $count = 0
    foreach ($relative in $files) {
        if ($relative -like ".github/*" -or $relative -like "Tools~/*") { continue }
        $src = Join-Path $repo $relative
        if (-not (Test-Path $src -PathType Leaf)) { continue }
        $dst = Join-Path $target $relative
        New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
        Copy-Item $src $dst
        $count++
    }
    $packageRef = "embedded ($count files copied to Packages/com.yaito3014.shiori)"
    $dependencyLine = ""
} elseif ($Absolute) {
    $packageRef = "file:" + ($repo -replace "\\", "/")
    $dependencyLine = ",`n    `"com.yaito3014.shiori`": `"$packageRef`""
} else {
    # Relative to <project>/Packages, which is how Unity resolves file: references.
    # Computed with .NET rather than Resolve-Path -Relative, which is not reliable on Linux.
    $relative = [System.IO.Path]::GetRelativePath($packagesDir, $repo)
    $packageRef = "file:" + ($relative -replace "\\", "/")
    $dependencyLine = ",`n    `"com.yaito3014.shiori`": `"$packageRef`""
}

$manifest = @"
{
  "dependencies": {
    "com.unity.test-framework": "1.1.33"$dependencyLine
  },
  "testables": [
    "com.yaito3014.shiori"
  ]
}
"@
# Join-Path (not backslashes) so the script also works on the Linux CI runner.
$manifestPath = Join-Path $packagesDir "manifest.json"
$versionPath = Join-Path (Join-Path $Destination "ProjectSettings") "ProjectVersion.txt"
[IO.File]::WriteAllText($manifestPath, $manifest.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($versionPath, "m_EditorVersion: $UnityVersion`n", [Text.UTF8Encoding]::new($false))

Write-Host "Dev project created: $Destination"
Write-Host "Package reference:   $packageRef"
