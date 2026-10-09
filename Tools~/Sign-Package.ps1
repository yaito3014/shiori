<#
.SYNOPSIS
  Builds a signed .tgz of a package repository with Unity 6.3+ (-upmPack), so Unity 6 stops
  reporting the package as "not signed". Run it yourself: it needs your Unity account password.

.DESCRIPTION
  The package content is exactly what the VPM zip ships: `git archive HEAD` of the repository,
  which honours the export-ignore attributes (Editor/, package.json, LICENSE). That copy is signed
  into <workspace>/dist/<name>-<version>.tgz, which contains the signature as .attestation.p7m.

  The password is read from $env:UNITY_PASSWORD when set, otherwise prompted for without echo.
  It is passed only to Unity.exe and never written to disk; Unity's own log may still contain the
  command line, so the log is deleted afterwards unless -KeepLog is given.

.PARAMETER PackageRoot
  Package repository. Default: the parent of this script (the core).

.PARAMETER Username
  Unity account email. Default: $env:UNITY_EMAIL.

.PARAMETER Organization
  Unity Cloud organization ID that signs the package. Default: $env:UNITY_ORG_ID.

.PARAMETER UnityVersion
  Editor used for signing; must be 6000.3 or newer. Default: 6000.6.0f1.

.EXAMPLE
  pwsh Tools~/Sign-Package.ps1 -Username you@example.com -Organization 15668133757639
  pwsh ../shiori/Tools~/Sign-Package.ps1 -PackageRoot . -Username you@example.com -Organization 15668133757639
#>
param(
    [string]$PackageRoot = "",
    [string]$Username = $env:UNITY_EMAIL,
    [string]$Organization = $env:UNITY_ORG_ID,
    [string]$UnityVersion = "6000.6.0f1",
    [string]$OutputDir = "",
    [switch]$KeepLog
)

$ErrorActionPreference = "Stop"

if ($PackageRoot -eq "") { $PackageRoot = Join-Path $PSScriptRoot ".." }
$PackageRoot = (Resolve-Path $PackageRoot).Path
$manifest = Get-Content (Join-Path $PackageRoot "package.json") -Raw | ConvertFrom-Json
$name = $manifest.name
$version = $manifest.version
if (-not $Username) { throw "Give -Username or set UNITY_EMAIL" }
if (-not $Organization) { throw "Give -Organization or set UNITY_ORG_ID" }

$unity = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $UnityVersion is not installed at $unity" }

if ($OutputDir -eq "") { $OutputDir = Join-Path (Split-Path $PackageRoot -Parent) "dist" }
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$output = Join-Path $OutputDir "$name-$version.tgz"

$password = $env:UNITY_PASSWORD
if (-not $password) {
    $secure = Read-Host "Unity password for $Username" -AsSecureString
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("shiori-sign-" + [Guid]::NewGuid().ToString("N"))
$source = Join-Path $work $name
$log = Join-Path $work "sign.log"
New-Item -ItemType Directory -Force $source | Out-Null
try {
    # Same content as the VPM zip: tracked files at HEAD minus export-ignore.
    $tar = Join-Path $env:SystemRoot "System32\tar.exe"
    $archive = Join-Path $work "package.tar"
    & git -C $PackageRoot archive --format=tar -o $archive HEAD
    if ($LASTEXITCODE -ne 0) { throw "git archive failed" }
    & $tar -xf $archive -C $source
    if ($LASTEXITCODE -ne 0) { throw "extracting the archive failed" }
    Remove-Item $archive

    if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    # -upmPack treats its second argument as a destination folder and names the tarball
    # <name>-<version>.tgz inside it, so Unity writes into a scratch folder and the file is moved.
    $packed = Join-Path $work "out"
    $arguments = @("-batchmode", "-nographics", "-username", $Username, "-password", $password,
        "-upmPack", $source, $packed, "-cloudOrganization", $Organization, "-logFile", $log)
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -NoNewWindow
    if (-not $process.WaitForExit(15 * 60 * 1000)) {
        Stop-Process -Id $process.Id -Force
        throw "Unity did not finish within 15 minutes"
    }

    $packageLines = Select-String -Path $log -Pattern '\[Package Manager\]' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Line }
    $produced = Get-ChildItem -Path $packed -Filter *.tgz -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($process.ExitCode -ne 0 -or -not $produced) {
        $packageLines | Select-Object -Last 10 | ForEach-Object { Write-Host $_ }
        throw "signing failed (exit code $($process.ExitCode))"
    }
    Move-Item $produced.FullName $output

    $entries = & $tar -tzf $output
    if (-not ($entries | Where-Object { $_ -like "*.attestation.p7m" })) { throw "the tarball has no .attestation.p7m; it is not signed" }
    Write-Host "Signed: $output"
    Write-Host ("SHA-256: " + (Get-FileHash $output -Algorithm SHA256).Hash.ToLowerInvariant())
}
finally {
    $password = $null
    if ($KeepLog -and (Test-Path $log)) {
        Copy-Item $log (Join-Path $OutputDir "$name-$version.sign.log")
    }
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
