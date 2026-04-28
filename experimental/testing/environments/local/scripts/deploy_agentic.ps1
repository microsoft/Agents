<#
.SYNOPSIS
    Step 1: Create an Agent Identity Blueprint.
    Step 2: Create an AgentIdentityBlueprintPrincipal.
    Step 3: Add a client secret to the blueprint.
    Step 4: Configure identifier URI and OAuth scope.
#>
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

$TenantId = $env:TENANT_ID
$EnvName  = $env:AZURE_ENV_NAME
$dotenv   = @{}

if (-not $TenantId -or -not $EnvName) {
    if (-not (Test-Path $DotEnvFile)) {
        Write-Error "TENANT_ID / AZURE_ENV_NAME not set and $DotEnvFile not found."
        exit 1
    }
    $dotenv   = Read-DotEnv $DotEnvFile
    $TenantId = if ($TenantId) { $TenantId } else { $dotenv['TENANT_ID'] }
    $EnvName  = if ($EnvName)  { $EnvName }  else { $dotenv['AZURE_ENV_NAME'] }
} elseif (Test-Path $DotEnvFile) {
    $dotenv = Read-DotEnv $DotEnvFile
}

$BlueprintName = "agents-$EnvName-blueprint"

Write-Host "Tenant    : $TenantId"
Write-Host "Blueprint : $BlueprintName"

Import-Module Microsoft.Graph.Authentication

Write-Host "Imported Microsoft.Graph.Authentication module."

Connect-MgGraph -Scopes "AgentIdentityBlueprint.Create",
                         "AgentIdentityBlueprintPrincipal.Create",
                         "AgentIdentityBlueprint.AddRemoveCreds.All",
                         "AgentIdentityBlueprint.UpdateAuthProperties.All",
                         "User.Read" `
                -TenantId $TenantId -NoWelcome

Write-Host "Connected to Microsoft Graph."

$me   = Invoke-MgGraphRequest -Uri "https://graph.microsoft.com/v1.0/me?`$select=id,displayName"
Write-Host "Signed in as : $($me.displayName) ($($me.id))"

# ---------------------------------------------------------------------------
# Step 1: Create the blueprint (idempotent via .env).
# ---------------------------------------------------------------------------
$BlueprintId        = $dotenv['AGENT_BLUEPRINT_ID']
$BlueprintAppId     = $dotenv['AGENT_BLUEPRINT_APP_ID']
$BlueprintPrincipalId = $dotenv['AGENT_BLUEPRINT_PRINCIPAL_ID']

if ($BlueprintId) {
    Write-Host "Blueprint already exists (from .env): $BlueprintId"
} else {
    $body = @{
        "@odata.type"         = "Microsoft.Graph.AgentIdentityBlueprint"
        "displayName"         = $BlueprintName
        "sponsors@odata.bind" = @("https://graph.microsoft.com/v1.0/users/$($me.id)")
    }

    $blueprint      = Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/beta/applications/" `
        -Headers @{ "OData-Version" = "4.0" } `
        -Body ($body | ConvertTo-Json)

    $BlueprintId    = $blueprint.id
    $BlueprintAppId = $blueprint.appId
    Write-Host "Blueprint created:"
    Write-Host "  id    = $BlueprintId"
    Write-Host "  appId = $BlueprintAppId"

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_BLUEPRINT_ID     = $BlueprintId
        AGENT_BLUEPRINT_APP_ID = $BlueprintAppId
    }
}

# ---------------------------------------------------------------------------
# Step 2: Create the AgentIdentityBlueprintPrincipal (idempotent via .env).
# ---------------------------------------------------------------------------
if ($BlueprintPrincipalId) {
    Write-Host "Blueprint principal already exists (from .env): $BlueprintPrincipalId"
} else {
    $principal = Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/beta/serviceprincipals/graph.agentIdentityBlueprintPrincipal" `
        -Headers @{ "OData-Version" = "4.0" } `
        -Body (@{ appId = $BlueprintAppId } | ConvertTo-Json)

    $BlueprintPrincipalId = $principal.id
    Write-Host "Blueprint principal created:"
    Write-Host "  id = $BlueprintPrincipalId"

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_BLUEPRINT_PRINCIPAL_ID = $BlueprintPrincipalId
    }
}

# ---------------------------------------------------------------------------
# Step 3: Add a client secret to the blueprint (idempotent via .env).
# The secret value is only returned at creation time — store it immediately.
# ---------------------------------------------------------------------------
$BlueprintSecret = $dotenv['AGENT_BLUEPRINT_SECRET']

if ($BlueprintSecret) {
    Write-Host "Blueprint secret already exists (from .env)."
} else {
    $secretBody = @{
        passwordCredential = @{
            displayName = "e2e-test-secret"
            endDateTime = (Get-Date).AddYears(1).ToString("o")
        }
    }

    $secretResponse  = Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/beta/applications/$BlueprintId/addPassword" `
        -Body ($secretBody | ConvertTo-Json -Depth 5)

    $BlueprintSecret = $secretResponse.secretText
    Write-Host "Blueprint secret created (expires $($secretResponse.endDateTime))."

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_BLUEPRINT_SECRET = $BlueprintSecret
    }
}

# ---------------------------------------------------------------------------
# Step 4: Configure identifier URI and OAuth scope (idempotent via .env).
# ---------------------------------------------------------------------------
$BlueprintScopeId = $dotenv['AGENT_BLUEPRINT_SCOPE_ID']

if ($BlueprintScopeId) {
    Write-Host "Blueprint identifier URI and scope already configured (from .env)."
} else {
    $BlueprintScopeId    = [guid]::NewGuid().ToString()
    $IdentifierUri       = "api://$BlueprintAppId"

    $patchBody = @{
        identifierUris = @($IdentifierUri)
        api            = @{
            oauth2PermissionScopes = @(@{
                adminConsentDescription = "Allow the application to access the agent on behalf of the signed-in user."
                adminConsentDisplayName = "Access agent"
                id                      = $BlueprintScopeId
                isEnabled               = $true
                type                    = "User"
                value                   = "access_agent"
            })
        }
    }

    Invoke-MgGraphRequest -Method PATCH `
        -Uri "https://graph.microsoft.com/beta/applications/$BlueprintId" `
        -Headers @{ "OData-Version" = "4.0" } `
        -Body ($patchBody | ConvertTo-Json -Depth 5) | Out-Null

    Write-Host "Identifier URI and scope configured:"
    Write-Host "  identifierUri = $IdentifierUri"
    Write-Host "  scopeId       = $BlueprintScopeId"

    Update-DotEnv -Path $DotEnvFile -Values @{
        AGENT_BLUEPRINT_SCOPE_ID = $BlueprintScopeId
    }
}

Write-Host "Deploying agent identity..."
& "$PSScriptRoot/deploy_agent_identity.ps1" -SponsorId $me.id
