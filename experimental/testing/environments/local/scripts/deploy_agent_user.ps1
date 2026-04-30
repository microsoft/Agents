<#
.SYNOPSIS
    Create an Agent ID User from an existing agent identity.

    Handles the full flow: admin consent for AgentIdUser.ReadWrite.IdentityParentedBy,
    blueprint token acquisition, user creation, and .env write-back.

    Prereqs (must be present in .env):
      TENANT_ID, AZURE_ENV_NAME, AGENT_BLUEPRINT_APP_ID, AGENT_BLUEPRINT_SECRET,
      AGENT_IDENTITY_ID
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$DotEnvFile = Resolve-Path "$PSScriptRoot/../.env"

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
    $lines = [System.Collections.Generic.List[string]]@(Get-Content $Path)
    foreach ($key in $Values.Keys) {
        $val = $Values[$key]
        $idx = $lines.FindIndex({ param($l) $l -match "^$key=" })
        if ($idx -ge 0) { $lines[$idx] = "$key=$val" } else { $lines.Add("$key=$val") }
    }
    $lines | Out-File $Path -Encoding utf8
}

$dotenv = Read-DotEnv $DotEnvFile

$TenantId             = $dotenv['TENANT_ID']
$EnvName              = $dotenv['AZURE_ENV_NAME']
$TenantDomain         = $dotenv['AGENT_TENANT_DOMAIN']
$BlueprintAppId       = $dotenv['AGENT_BLUEPRINT_APP_ID']
$BlueprintSecret      = $dotenv['AGENT_BLUEPRINT_SECRET']
$BlueprintPrincipalId = $dotenv['AGENT_BLUEPRINT_PRINCIPAL_ID']
$AgentIdentityId      = $dotenv['AGENT_IDENTITY_ID']

foreach ($pair in @(
    @('TENANT_ID',                   $TenantId),
    @('AZURE_ENV_NAME',              $EnvName),
    @('AGENT_TENANT_DOMAIN',         $TenantDomain),
    @('AGENT_BLUEPRINT_APP_ID',      $BlueprintAppId),
    @('AGENT_BLUEPRINT_SECRET',      $BlueprintSecret),
    @('AGENT_BLUEPRINT_PRINCIPAL_ID',$BlueprintPrincipalId),
    @('AGENT_IDENTITY_ID',           $AgentIdentityId)
)) {
    if (-not $pair[1]) {
        Write-Error "$($pair[0]) is required but missing. Ensure deploy_agent_identity.ps1 has run successfully."
        exit 1
    }
}

if ($dotenv['AGENT_USER_ID']) {
    Write-Host "Agent user already exists (from .env): $($dotenv['AGENT_USER_ID'])"
    exit 0
}

# ---------------------------------------------------------------------------
# Step 1: Grant AgentIdUser.ReadWrite.IdentityParentedBy to the blueprint principal.
# Requires AgentIdentityBlueprint.ReadWrite.All — the blueprint-specific scope
# needed to manage app role assignments on agent blueprint service principals.
# ---------------------------------------------------------------------------
if (-not $dotenv['AGENT_BLUEPRINT_AGENTIDUSER_ROLE_ASSIGNED']) {
    $agentIdUserRoleId = "4aa6e624-eee0-40ab-bdd8-f9639038a614"

    $graphSpId = az ad sp show --id '00000003-0000-0000-c000-000000000000' --query id -o tsv
    if (-not $graphSpId) {
        Write-Error "Could not resolve Microsoft Graph service principal. Ensure 'az login' has been run."
        exit 1
    }

    Import-Module Microsoft.Graph.Authentication
    Connect-MgGraph -Scopes "AgentIdentityBlueprint.ReadWrite.All" `
                    -TenantId $TenantId -NoWelcome

    Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$BlueprintPrincipalId/appRoleAssignments" `
        -Body (@{
            principalId = $BlueprintPrincipalId
            resourceId  = $graphSpId
            appRoleId   = $agentIdUserRoleId
        } | ConvertTo-Json) | Out-Null

    Disconnect-MgGraph | Out-Null

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_BLUEPRINT_AGENTIDUSER_ROLE_ASSIGNED = "true"
    }
    Write-Host "AgentIdUser.ReadWrite.IdentityParentedBy granted to blueprint principal."
}

# ---------------------------------------------------------------------------
# Step 2: Acquire a blueprint token (client credentials).
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
# Step 3: Create the agent user.
# ---------------------------------------------------------------------------
$Nickname = "agents-$EnvName-user"
$Upn      = "$Nickname@$TenantDomain"

Write-Host "Creating agent user: $Upn"

$userBody = @{
    "@odata.type"     = "Microsoft.Graph.AgentUser"
    displayName       = $Nickname
    userPrincipalName = $Upn
    identityParentId  = $AgentIdentityId
    mailNickname      = $Nickname
    accountEnabled    = $true
} | ConvertTo-Json -Depth 5

$user = Invoke-RestMethod `
    -Uri "https://graph.microsoft.com/beta/users" `
    -Method POST `
    -Headers @{
        Authorization  = "Bearer $BlueprintToken"
        "Content-Type" = "application/json"
    } `
    -Body $userBody

Write-Host "Agent user created:"
Write-Host "  id  = $($user.id)"
Write-Host "  upn = $($user.userPrincipalName)"

Update-DotEnv -Path $DotEnvFile -Values @{
    AGENT_USER_ID  = $user.id
    AGENT_USER_UPN = $user.userPrincipalName
}
