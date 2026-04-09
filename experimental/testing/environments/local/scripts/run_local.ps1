<#
.SYNOPSIS
    Provision (optionally) and run the environments/local test suite in Docker.

.DESCRIPTION
    1. [Optional] Provisions Azure resources via provision.ps1.
    2. Verifies that agent .env files exist (fail-fast if provision hasn't run).
    3. Builds tests.Dockerfile (tagged agents-test).
    4. Runs pytest inside the container with the repo mounted at /repo.

    Run from anywhere — the script resolves all paths from its own location.

.EXAMPLE
    # Provision first, then test:
    ./environments/local/scripts/run_local.ps1 -Provision -ResourceGroup rg-e2e-local

    # Skip provision (resources already exist), run tests only:
    ./environments/local/scripts/run_local.ps1

    # Skip Docker build (reuse existing image):
    ./environments/local/scripts/run_local.ps1 -NoBuild

.PARAMETER Provision
    Provision Azure resources before running tests. Requires -ResourceGroup.

.PARAMETER ResourceGroup
    Azure resource group (required with -Provision).

.PARAMETER EnvironmentName
    Passed to provision.ps1. Default: e2e-local.

.PARAMETER Location
    Azure region. Default: eastus.

.PARAMETER Scenario
    Agent scenario under environments/local/agents/. Default: quickstart.

.PARAMETER TestPath
    pytest path to run inside the container. Default: environments/local/tests/

.PARAMETER NoBuild
    Skip the docker build step (reuse the existing agents-test image).
#>
param(
    [switch]$Provision,

    [string]$ResourceGroup = "",

    [string]$EnvironmentName = "e2e-local",

    [string]$Location = "eastus",

    [string]$Scenario = "quickstart",

    [string]$TestPath = "environments/local/tests/",

    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir  = (Resolve-Path "$PSScriptRoot/..").Path           # environments/local/
$RepoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path    # testing/

Push-Location $RepoRoot
try {
    # ------------------------------------------------------------------ #
    # 1. Optional provision                                               #
    # ------------------------------------------------------------------ #
    if ($Provision) {
        if (-not $ResourceGroup) {
            Write-Error "-ResourceGroup is required when using -Provision."
            exit 1
        }
        Write-Host "Provisioning Azure resources..."
        & "$PSScriptRoot/provision.ps1" `
            -ResourceGroup $ResourceGroup `
            -EnvironmentName $EnvironmentName `
            -Location $Location `
            -Scenario $Scenario
    }

    # ------------------------------------------------------------------ #
    # 2. Pre-flight: verify agent configs exist                           #
    # ------------------------------------------------------------------ #
    $requiredConfigs = @(
        "$EnvDir/agents/$Scenario/python/.env",
        "$EnvDir/agents/$Scenario/js/.env",
        "$EnvDir/agents/$Scenario/net/appsettings.local.json"
    )
    $missing = $requiredConfigs | Where-Object { -not (Test-Path $_) }
    if ($missing) {
        Write-Error (
            "Missing agent config files:`n" +
            ($missing -join "`n") +
            "`n`nRun provision.ps1 first, or pass -Provision -ResourceGroup <rg>."
        )
        exit 1
    }

    # ------------------------------------------------------------------ #
    # 3. Build Docker image                                               #
    # ------------------------------------------------------------------ #
    if (-not $NoBuild) {
        Write-Host "Building agents-test image..."
        docker build -f environments/local/tests.Dockerfile -t agents-test .
    }

    # ------------------------------------------------------------------ #
    # 4. Run tests                                                        #
    # ------------------------------------------------------------------ #
    Write-Host "Running tests: $TestPath"
    docker run --rm `
        -v "${RepoRoot}:/repo" `
        agents-test `
        $TestPath

} finally {
    Pop-Location
}
