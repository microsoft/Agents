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
        .\scripts\setup_a365.ps1 -AgentName my-agent-name

    If -AgentName is omitted, the script prompts for it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$AgentName
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir              = (Resolve-Path "$PSScriptRoot/..").Path
$ConfigPath          = Join-Path $EnvDir 'a365.config.json'
$GeneratedConfigPath = Join-Path $EnvDir 'a365.generated.config.json'
$DotEnvFile          = Join-Path $EnvDir '.env'
Push-Location $EnvDir

try {
    # ── 1. Blueprint, permissions, and agent registration ────────────────────
    Write-Host "Running a365 setup..."
    a365 setup all --verbose --agent-name $AgentName
    if ($LASTEXITCODE -ne 0) { throw "a365 setup all failed." }

    # ── 2. Inject agent user fields required by create-instance ──────────────
    # `a365 setup all` writes a365.config.json without these fields, but
    # `a365 create-instance` needs them to create the Azure AD agent user.
    Write-Host "Adding agent user fields to a365.config.json..."

    $TenantDomain = az rest --method GET `
        --uri 'https://graph.microsoft.com/v1.0/domains' `
        --query 'value[?isDefault].id | [0]' --output tsv
    if ($LASTEXITCODE -ne 0 -or -not $TenantDomain) {
        throw "Could not resolve the tenant's default verified domain via Graph."
    }

    $Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    $Config | Add-Member -NotePropertyName 'agentUserDisplayName'   -NotePropertyValue "$AgentName User"            -Force
    $Config | Add-Member -NotePropertyName 'agentUserPrincipalName' -NotePropertyValue "$AgentName@$TenantDomain"   -Force
    $Config | Add-Member -NotePropertyName 'agentUserUsageLocation' -NotePropertyValue 'US'                         -Force
    $Config | ConvertTo-Json -Depth 10 | Set-Content $ConfigPath -Encoding utf8NoBOM

    # # ── 3. Publish the agent ─────────────────────────────────────────────────
    # # Required prerequisite before create-instance can run.
    # Write-Host "Publishing agent..."
    # a365 publish
    # if ($LASTEXITCODE -ne 0) { throw "a365 publish failed." }

    # ── 4. Create agent identity and agent user ──────────────────────────────
    # Creates the Entra service principal (agent identity) and the Azure AD
    # agent user account. Skips license assignment — run create-instance licenses
    # separately once Agent 365 licenses are available in the tenant.
    Write-Host "Creating agent instance (identity + user, no licenses)..."
    a365 create-instance --verbose --agent-name $AgentName
    if ($LASTEXITCODE -ne 0) { throw "a365 create-instance failed." }

    # ── 5. Copy agent identity values from config files into .env ────────────
    # downstream agent config (env.TEMPLATE, appsettings.json) references these
    # as ${{ AGENT_BLUEPRINT_ID }}, ${{ AGENT_BLUEPRINT_SECRET }}, etc.
    Write-Host "Writing agent identity values to $DotEnvFile..."
    $Generated = Get-Content $GeneratedConfigPath -Raw | ConvertFrom-Json
    $Config    = Get-Content $ConfigPath          -Raw | ConvertFrom-Json
    $AgentValues = [ordered]@{
        AGENT_BLUEPRINT_ID     = $Generated.agentBlueprintObjectId
        AGENT_BLUEPRINT_SECRET = $Generated.agentBlueprintClientSecret
        AGENT_INSTANCE_ID      = $Generated.AgenticAppId
        AGENT_USER_ID          = $Generated.AgenticUserId
        AGENT_UPN              = $Config.agentUserPrincipalName
    }
    foreach ($kv in $AgentValues.GetEnumerator()) {
        if (-not $kv.Value) { throw "$($kv.Key) is missing from config files." }
    }

    $existing = [ordered]@{}
    if (Test-Path $DotEnvFile) {
        foreach ($line in Get-Content $DotEnvFile) {
            $trimmed = $line.Trim()
            if ($trimmed -and -not $trimmed.StartsWith('#') -and $trimmed -match '^([^=]+)=(.*)$') {
                $existing[$Matches[1].Trim()] = $Matches[2].Trim()
            }
        }
    }
    foreach ($kv in $AgentValues.GetEnumerator()) { $existing[$kv.Key] = $kv.Value }
    $existing.GetEnumerator() | Sort-Object Key | ForEach-Object { "$($_.Key)=$($_.Value)" } |
        Out-File $DotEnvFile -Encoding utf8

    Write-Host ""
    Write-Host "Agent instance created. To assign licenses when available:"
    Write-Host "  a365 create-instance licenses"
} finally {
    Pop-Location
}
