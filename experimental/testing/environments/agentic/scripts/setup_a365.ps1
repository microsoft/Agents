<#
.SYNOPSIS
    Full A365 agent setup: blueprint, publishing, and agent instance creation.

.DESCRIPTION
    Runs the A365 CLI commands required to set up a complete agent instance:

      1. a365 setup all       — creates blueprint, registers agent, grants permissions
      2. a365 publish         — publishes the agent (required before create-instance)
      3. a365 create-instance identity
                              — creates the Azure AD agent identity (service principal)
                                and the agent user account, then sets manager/sponsor.
                                Skips license assignment; run `a365 create-instance licenses`
                                separately once Agent 365 licenses are available.

    Requires Global Administrator to create the agent user and assign licenses.
    Reads a365.config.json and a365.generated.config.json from the working directory.

    Run from environments/agentic/:
        .\scripts\setup_a365.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir = (Resolve-Path "$PSScriptRoot/..").Path
Push-Location $EnvDir

try {
    # ── 1. Blueprint, permissions, and agent registration ────────────────────
    Write-Host "Running a365 setup..."
    a365 setup all --verbose --agent-name "agents-e2e-agentic-agent"
    if ($LASTEXITCODE -ne 0) { throw "a365 setup all failed." }

    # # ── 2. Publish the agent ─────────────────────────────────────────────────
    # # Required prerequisite before create-instance can run.
    # Write-Host "Publishing agent..."
    # a365 publish
    # if ($LASTEXITCODE -ne 0) { throw "a365 publish failed." }

    # ── 3. Create agent identity and agent user ──────────────────────────────
    # Creates the Entra service principal (agent identity) and the Azure AD
    # agent user account. Skips license assignment — run create-instance licenses
    # separately once Agent 365 licenses are available in the tenant.
    Write-Host "Creating agent instance (identity + user, no licenses)..."
    a365 create-instance --verbose --agent-name "agents-e2e-agentic-agent"
    if ($LASTEXITCODE -ne 0) { throw "a365 create-instance failed." }

    Write-Host ""
    Write-Host "Agent instance created. To assign licenses when available:"
    Write-Host "  a365 create-instance licenses"
} finally {
    Pop-Location
}
