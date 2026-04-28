<#
.SYNOPSIS
    Step 5: Create an Agent Identity from the blueprint.

    Phase 2 of the agentic identity provisioning flow. The blueprint authenticates
    using its own client credentials, then uses that token to create an agentIdentity
    service principal derived from the blueprint.

    Prereqs: deploy_agentic.ps1 must have run successfully and the following keys
    must be present in the .env file:
      TENANT_ID, AZURE_ENV_NAME, AGENT_BLUEPRINT_APP_ID, AGENT_BLUEPRINT_SECRET
#>
param(
    [Parameter(Mandatory)][string]$SponsorId
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir     = (Resolve-Path "$PSScriptRoot/..").Path
$DotEnvFile = Join-Path $EnvDir '.env'

function Read-DotEnv {
    param([string]$Path)
    $map = @{}
    foreach ($line in Get-Content $Path) {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line -match '^([^=]+)=(.*)$') {
            $map[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
    return $map
}

function Update-DotEnv {
    param([string]$Path, [hashtable]$Values)
    $lines = [System.Collections.Generic.List[string]]@(
        if (Test-Path $Path) { @(Get-Content $Path) } else { @() }
    )
    foreach ($key in $Values.Keys) {
        $val = $Values[$key]
        $idx = $lines.FindIndex({ param($l) $l -match "^$key=" })
        if ($idx -ge 0) { $lines[$idx] = "$key=$val" } else { $lines.Add("$key=$val") }
    }
    $lines | Out-File $Path -Encoding utf8
}

if (-not (Test-Path $DotEnvFile)) {
    Write-Error ".env not found at $DotEnvFile — run deploy_agentic.ps1 first."
    exit 1
}
$dotenv = Read-DotEnv $DotEnvFile

$TenantId        = if ($env:TENANT_ID)             { $env:TENANT_ID }             else { $dotenv['TENANT_ID'] }
$EnvName         = if ($env:AZURE_ENV_NAME)         { $env:AZURE_ENV_NAME }         else { $dotenv['AZURE_ENV_NAME'] }
$BlueprintAppId  = if ($env:AGENT_BLUEPRINT_APP_ID) { $env:AGENT_BLUEPRINT_APP_ID } else { $dotenv['AGENT_BLUEPRINT_APP_ID'] }
$BlueprintSecret = if ($env:AGENT_BLUEPRINT_SECRET) { $env:AGENT_BLUEPRINT_SECRET } else { $dotenv['AGENT_BLUEPRINT_SECRET'] }

foreach ($pair in @(
    @('TENANT_ID',              $TenantId),
    @('AZURE_ENV_NAME',         $EnvName),
    @('AGENT_BLUEPRINT_APP_ID', $BlueprintAppId),
    @('AGENT_BLUEPRINT_SECRET', $BlueprintSecret)
)) {
    if (-not $pair[1]) {
        Write-Error "$($pair[0]) is required but was not found. Ensure deploy_agentic.ps1 has run successfully."
        exit 1
    }
}

$IdentityDisplayName = "agents-$EnvName-identity"

Write-Host "Tenant   : $TenantId"
Write-Host "Identity : $IdentityDisplayName"

# ---------------------------------------------------------------------------
# Acquire a token using the blueprint's own credentials (client credentials).
# Phase 2 uses the blueprint app as the caller — no interactive sign-in needed.
# ---------------------------------------------------------------------------
Write-Host "Acquiring blueprint token..."

$tokenResponse = Invoke-RestMethod `
    -Uri "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token" `
    -Method POST `
    -Body @{
        client_id     = $BlueprintAppId
        client_secret = $BlueprintSecret
        scope         = "https://graph.microsoft.com/.default"
        grant_type    = "client_credentials"
    }

$BlueprintToken = $tokenResponse.access_token
Write-Host "Blueprint token acquired."

# ---------------------------------------------------------------------------
# Create the agent identity (idempotent via .env).
# ---------------------------------------------------------------------------
$AgentIdentityId = $dotenv['AGENT_IDENTITY_ID']

if ($AgentIdentityId) {
    Write-Host "Agent identity already exists (from .env): $AgentIdentityId"
} else {
    $identityBody = @{
        displayName              = $IdentityDisplayName
        agentIdentityBlueprintId = $BlueprintAppId
        "sponsors@odata.bind"    = @("https://graph.microsoft.com/v1.0/users/$SponsorId")
    } | ConvertTo-Json -Depth 5

    $identity = Invoke-RestMethod `
        -Uri "https://graph.microsoft.com/beta/serviceprincipals/Microsoft.Graph.AgentIdentity" `
        -Method POST `
        -Headers @{
            Authorization   = "Bearer $BlueprintToken"
            "OData-Version" = "4.0"
            "Content-Type"  = "application/json"
        } `
        -Body $identityBody

    $AgentIdentityId = $identity.id
    Write-Host "Agent identity created:"
    Write-Host "  id = $AgentIdentityId"

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_IDENTITY_ID = $AgentIdentityId
    }
}
