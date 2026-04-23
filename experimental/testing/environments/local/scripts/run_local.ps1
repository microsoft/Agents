<#
.SYNOPSIS
    Run the test suite in Docker.

.DESCRIPTION
    1. Verifies that agent .env files exist (fail-fast if azd provision hasn't run).
    2. Builds tests.Dockerfile (tagged agents-test).
    3. Runs pytest inside the container.

    Run from anywhere — the script resolves all paths from its own location.

.EXAMPLE
    # Run tests:
    ./scripts/run_local.ps1

    # Skip Docker build (reuse existing image):
    ./scripts/run_local.ps1 -NoBuild

.PARAMETER Scenario
    Agent scenario glob under agents/. Default: * (all scenarios).

.PARAMETER TestPath
    pytest path to run inside the container. Default: tests/

.PARAMETER NoBuild
    Skip the docker build step (reuse the existing agents-test image).
#>
param(
    [string]$Scenario = "*",

    [string]$TestPath = "tests/",

    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir = (Resolve-Path "$PSScriptRoot/..").Path

Push-Location $EnvDir
try {
    # ------------------------------------------------------------------ #
    # 1. Pre-flight: verify agent configs exist                           #
    # ------------------------------------------------------------------ #
    $scenarioDirs = Get-ChildItem "$EnvDir/agents" -Directory | Where-Object { $_.Name -like $Scenario }
    if (-not $scenarioDirs) {
        Write-Error "No scenario directories matched: $Scenario"
        exit 1
    }
    $missing = @()
    foreach ($dir in $scenarioDirs) {
        Get-ChildItem $dir.FullName -Recurse -Filter 'env.TEMPLATE' | ForEach-Object {
            $out = Join-Path $_.DirectoryName '.env'
            if (-not (Test-Path $out)) { $missing += $out }
        }
        Get-ChildItem $dir.FullName -Recurse -Filter 'appsettings.json' | ForEach-Object {
            if (Select-String -Path $_.FullName -Pattern '\$\{\{' -Quiet) {
                $out = Join-Path $_.DirectoryName 'appsettings.local.json'
                if (-not (Test-Path $out)) { $missing += $out }
            }
        }
    }
    if ($missing) {
        Write-Error (
            "Missing agent config files:`n" +
            ($missing -join "`n") +
            "`n`nRun 'azd provision' first."
        )
        exit 1
    }

    # ------------------------------------------------------------------ #
    # 2. Build Docker image                                               #
    # ------------------------------------------------------------------ #
    if (-not $NoBuild) {
        Write-Host "Building agents-test image..."
        docker build -f tests.Dockerfile -t agents-test .
    }

    # ------------------------------------------------------------------ #
    # 3. Run tests                                                        #
    # ------------------------------------------------------------------ #
    Write-Host "Running tests: $TestPath"
    docker run --rm `
        -v "${EnvDir}:/repo" `
        agents-test `
        $TestPath

} finally {
    Pop-Location
}
