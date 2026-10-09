<#
.SYNOPSIS
  Creates a minimal Unity project that references this package, outside the repository
  by default so the setup wizard can run "git init" in it.

.PARAMETER UnityVersion
  Full editor version, e.g. 2022.3.22f1 or 6000.6.0f1.

.PARAMETER Destination
  Where to create the project. Default: <repo parent>/shiori-dev/<major.minor>.

.PARAMETER Absolute
  Reference the package by absolute path instead of a path relative to the project.

.EXAMPLE
  pwsh Tools~/New-DevProject.ps1 -UnityVersion 2022.3.22f1
  pwsh Tools~/New-DevProject.ps1 -UnityVersion 6000.6.0f1 -Destination ci-project/6000.6.0f1
#>
param(
    [Parameter(Mandatory = $true)] [string]$UnityVersion,
    [string]$Destination = "",
    [switch]$Absolute
)

$ErrorActionPreference = "Stop"

if ($UnityVersion -notmatch '^(\d+)\.(\d+)\.\d+[abfp]\d+$') {
    throw "UnityVersion must look like 2022.3.22f1 (got '$UnityVersion')"
}
$stream = "$($Matches[1]).$($Matches[2])"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ($Destination -eq "") {
    $Destination = Join-Path (Split-Path $repo -Parent) "shiori-dev\$stream"
}
if (Test-Path $Destination) { throw "Destination already exists: $Destination" }

New-Item -ItemType Directory -Force (Join-Path $Destination "Assets") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $Destination "Packages") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $Destination "ProjectSettings") | Out-Null
$Destination = (Resolve-Path $Destination).Path

if ($Absolute) {
    $packageRef = "file:" + ($repo -replace "\\", "/")
} else {
    # Relative to <project>/Packages, which is how Unity resolves file: references.
    Push-Location (Join-Path $Destination "Packages")
    try { $packageRef = "file:" + ((Resolve-Path -Relative $repo) -replace "\\", "/") }
    finally { Pop-Location }
}

$manifest = @"
{
  "dependencies": {
    "com.unity.test-framework": "1.1.33",
    "com.yaito3014.shiori": "$packageRef"
  },
  "testables": [
    "com.yaito3014.shiori"
  ]
}
"@
[IO.File]::WriteAllText((Join-Path $Destination "Packages\manifest.json"), $manifest.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Destination "ProjectSettings\ProjectVersion.txt"), "m_EditorVersion: $UnityVersion`n", [Text.UTF8Encoding]::new($false))

Write-Host "Dev project created: $Destination"
Write-Host "Package reference:   $packageRef"
