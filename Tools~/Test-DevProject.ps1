<#
.SYNOPSIS
  Runs the EditMode tests of a dev project with the matching Unity editor and prints a summary.

.PARAMETER UnityVersion
  Full editor version installed through Unity Hub, e.g. 2022.3.22f1.

.PARAMETER ProjectPath
  The dev project. Default: <repo parent>/shiori-dev/<major.minor>.

.PARAMETER CompileOnly
  Open the project in batch mode and quit without running tests.

.EXAMPLE
  pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1
  pwsh Tools~/Test-DevProject.ps1 -UnityVersion 6000.6.0f1 -CompileOnly
#>
param(
    [Parameter(Mandatory = $true)] [string]$UnityVersion,
    [string]$ProjectPath = "",
    [string]$LogFile = "",
    [int]$TimeoutMinutes = 15,
    [switch]$CompileOnly
)

$ErrorActionPreference = "Stop"

if ($UnityVersion -notmatch '^(\d+)\.(\d+)\.') { throw "UnityVersion must look like 2022.3.22f1" }
$stream = "$($Matches[1]).$($Matches[2])"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ($ProjectPath -eq "") { $ProjectPath = Join-Path (Join-Path (Split-Path $repo -Parent) "shiori-dev") $stream }
if (-not (Test-Path $ProjectPath)) { throw "Dev project not found: $ProjectPath (create it with New-DevProject.ps1)" }
$ProjectPath = (Resolve-Path $ProjectPath).Path

$unity = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $UnityVersion is not installed at $unity" }

# Outputs go under Logs/, which the Shiori .gitignore block ignores, so they never show up as changes.
$outDir = Join-Path (Join-Path $ProjectPath "Logs") "shiori-tests"
New-Item -ItemType Directory -Force $outDir | Out-Null
if ($LogFile -eq "") { $LogFile = Join-Path $outDir "unity-test.log" }
$results = Join-Path $outDir "TestResults.xml"
if (Test-Path $results) { Remove-Item $results }

$args = @("-batchmode", "-nographics", "-projectPath", $ProjectPath, "-logFile", $LogFile)
if ($CompileOnly) {
    $args += "-quit"
} else {
    $args += @("-runTests", "-testPlatform", "EditMode", "-testResults", $results)
}

$process = Start-Process -FilePath $unity -ArgumentList $args -PassThru -NoNewWindow
if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    # A hung editor (for example a test that blocks the main thread on a task) would otherwise wait forever.
    Write-Host "Unity did not exit within $TimeoutMinutes minutes; killing it. See $LogFile"
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "ParentProcessId = $($process.Id)" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    exit 124
}
$exit = $process.ExitCode

$errors = Select-String -Path $LogFile -Pattern '(error|warning) CS\d{4}' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
if ($errors) {
    Write-Host "Compiler messages:"
    $errors | ForEach-Object { Write-Host "  $_" }
}

if ($CompileOnly) {
    Write-Host "Compile exit code: $exit"
    exit $exit
}

if (-not (Test-Path $results)) {
    Write-Host "No test results were written (exit code $exit). See $LogFile"
    exit 1
}

[xml]$xml = Get-Content $results
$run = $xml.'test-run'
Write-Host ("Unity {0}: total={1} passed={2} failed={3} inconclusive={4} skipped={5}" -f $UnityVersion, $run.total, $run.passed, $run.failed, $run.inconclusive, $run.skipped)
$failed = $xml.SelectNodes("//test-case[@result='Failed']")
foreach ($case in $failed) {
    Write-Host "  FAILED $($case.fullname)"
    $message = $case.failure.message.'#cdata-section'
    if ($message) { Write-Host "    " + ($message.Trim() -replace "`n", "`n    ") }
}
exit ([int]$run.failed -gt 0 ? 1 : 0)
