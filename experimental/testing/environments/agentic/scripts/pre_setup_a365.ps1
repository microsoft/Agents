<#
.SYNOPSIS
    Finalizes the A365 CLI app registration after bicep provisioning.

.DESCRIPTION
    Adds configuration that cannot be expressed in Bicep:
      - WAM redirect URI: ms-appx-web://Microsoft.AAD.BrokerPlugin/{client-id}
        (requires the app's own client ID — circular reference in Bicep)
      - Tenant-wide admin consent for all 14 delegated permissions via a single
        oauth2PermissionGrant against the Microsoft Graph service principal.
        All 14 permissions (including the beta AgentIdentity*/AgentInstance*/
        AgentRegistration* scopes) belong to Microsoft Graph
        (resourceAppId 00000003-0000-0000-c000-000000000000).

    Run automatically by postprovision.ps1 after `azd provision`.
    Can also be run manually: .\pre_setup_a365.ps1

.NOTES
    Granting User.ReadWrite.All with AllPrincipals consent requires
    Global Administrator. All other permissions require at minimum
    Application Administrator or Cloud Application Administrator.

    WARNING: After this script runs, do NOT click "Grant admin consent" in the
    Entra admin center. The portal only sees non-beta permissions and will
    overwrite the grant, deleting the beta AgentIdentity/AgentInstance scopes.
    Re-run this script to restore accidentally deleted permissions.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir     = (Resolve-Path "$PSScriptRoot/..").Path
$DotEnvFile = Join-Path $EnvDir '.env'

function Read-DotEnv {
    param([string]$Path, [string]$Key)
    foreach ($line in Get-Content $Path) {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line -match "^$Key=(.*)$") {
            return $Matches[1].Trim()
        }
    }
    return $null
}

# Invoke az rest and return parsed JSON. Throws on non-zero exit code.
# Bodies are written to a temp file and passed as @<path> to avoid shell
# quoting/escaping issues with JSON strings containing spaces or special chars.
function Invoke-GraphRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Body      = $null,
        [hashtable]$Headers = @{}
    )
    $azArgs = @('rest', '--method', $Method, '--uri', $Uri)

    $tempFile = $null
    if ($Body) {
        $tempFile = [System.IO.Path]::GetTempFileName()
        Set-Content -Path $tempFile -Value $Body -Encoding utf8NoBOM
        $azArgs += '--body', "@$tempFile"
    }

    foreach ($kv in $Headers.GetEnumerator()) {
        $azArgs += '--headers', "$($kv.Key)=$($kv.Value)"
    }

    try {
        $output = az @azArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Graph API call failed ($Method $Uri): $output"
        }
        if ($output) { return ($output | ConvertFrom-Json) }
        return $null
    } finally {
        if ($tempFile -and (Test-Path $tempFile)) { Remove-Item $tempFile }
    }
}

# All 14 required delegated permissions — all scoped to Microsoft Graph.
# Source: https://learn.microsoft.com/en-us/microsoft-agent-365/developer/custom-client-app-registration
# Note: User.ReadWrite.All requires Global Administrator to consent for AllPrincipals.
$RequiredScope = 'AgentIdentityBlueprintPrincipal.Create ' +
                 'AgentIdentityBlueprint.ReadWrite.All ' +
                 'AgentIdentityBlueprint.UpdateAuthProperties.All ' +
                 'AgentIdentityBlueprint.AddRemoveCreds.All ' +
                 'AgentIdentityBlueprint.DeleteRestore.All ' +
                 'AgentInstance.ReadWrite.All ' +
                 'AgentIdentity.Create.All ' +
                 'AgentIdentity.DeleteRestore.All ' +
                 'AgentIdentity.Read.All ' +
                 'AgentRegistration.ReadWrite.All ' +
                 'DelegatedPermissionGrant.ReadWrite.All ' +
                 'Directory.Read.All ' +
                 'User.Read ' +
                 'User.ReadWrite.All'

# A365_APP_ID is set by azd from Bicep output when running as a hook.
# Fall back to .env on manual runs.
$A365AppId = $env:A365_APP_ID
if (-not $A365AppId) {
    if (-not (Test-Path $DotEnvFile)) {
        Write-Error "A365_APP_ID is not set and $DotEnvFile does not exist. Run 'azd provision' first."
        exit 1
    }
    $A365AppId = Read-DotEnv -Path $DotEnvFile -Key 'A365_APP_ID'
}
if (-not $A365AppId) {
    Write-Error "A365_APP_ID not found in environment variables or $DotEnvFile."
    exit 1
}

Write-Host "Configuring A365 CLI app registration: $A365AppId"

$AppObjectId = az ad app show --id $A365AppId --query id --output tsv
if ($LASTEXITCODE -ne 0 -or -not $AppObjectId) {
    Write-Error "Could not find app registration with id '$A365AppId'."
    exit 1
}

# ── 1. WAM redirect URI ──────────────────────────────────────────────────────
# ms-appx-web://Microsoft.AAD.BrokerPlugin/{client-id} enables Web Account
# Manager (WAM) on Windows — requires the app's own client ID so it cannot be
# set in Bicep without a circular reference.

Write-Host "Adding WAM redirect URI..."
$WamUri = "ms-appx-web://Microsoft.AAD.BrokerPlugin/$A365AppId"

$CurrentUrisJson = az ad app show --id $A365AppId `
    --query 'publicClient.redirectUris' --output json
$CurrentUris = if ($CurrentUrisJson -and $CurrentUrisJson.Trim() -ne 'null') {
    @($CurrentUrisJson | ConvertFrom-Json)
} else { @() }

if ($WamUri -notin $CurrentUris) {
    $AllUris = $CurrentUris + $WamUri
    # az ad app update takes URIs as individual arguments — avoids JSON body construction.
    az ad app update --id $AppObjectId --public-client-redirect-uris @AllUris
    if ($LASTEXITCODE -ne 0) { throw "Failed to update public client redirect URIs." }
    Write-Host "  Added: $WamUri"
} else {
    Write-Host "  Already present: $WamUri"
}

# ── 2. Get SP_OBJECT_ID (service principal for this app) ─────────────────────
# The oauth2PermissionGrant requires the SP object ID, not the app/client ID.

Write-Host "Resolving service principal IDs..."

$SpResult = Invoke-GraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=appId eq '$A365AppId'&`$select=id"

$SpObjectId = if ($SpResult.value -and $SpResult.value.Count -gt 0) {
    $SpResult.value[0].id
} else { $null }

if (-not $SpObjectId) {
    Write-Host "  Creating service principal for A365 CLI app..."
    $NewSp = Invoke-GraphRequest -Method POST `
        -Uri     'https://graph.microsoft.com/v1.0/servicePrincipals' `
        -Body    "{`"appId`":`"$A365AppId`"}" `
        -Headers @{ 'Content-Type' = 'application/json' }
    $SpObjectId = $NewSp.id
}
Write-Host "  SP_OBJECT_ID  : $SpObjectId"

# ── 3. Get GRAPH_RESOURCE_ID (Microsoft Graph service principal object ID) ───
# All 13 permissions — including the beta AgentIdentity/AgentInstance scopes —
# belong to Microsoft Graph (appId 00000003-0000-0000-c000-000000000000).

$GraphResult = Invoke-GraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=appId eq '00000003-0000-0000-c000-000000000000'&`$select=id"
$GraphResourceId = $GraphResult.value[0].id
Write-Host "  GRAPH_RESOURCE_ID : $GraphResourceId"

# ── 4. Grant tenant-wide admin consent via oauth2PermissionGrant ─────────────
# A single grant covers all 13 scopes. Using POST and handling the
# Request_MultipleObjectsWithSameKeyValue case with PATCH (update existing grant).

Write-Host "Granting tenant-wide admin consent for all 14 permissions..."

$ExistingGrants = Invoke-GraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants?`$filter=clientId eq '$SpObjectId' and resourceId eq '$GraphResourceId'"

$Grant = if ($ExistingGrants.value -and $ExistingGrants.value.Count -gt 0) {
    Write-Host "  Existing grant found — updating scope..."
    Invoke-GraphRequest -Method PATCH `
        -Uri     "https://graph.microsoft.com/v1.0/oauth2PermissionGrants/$($ExistingGrants.value[0].id)" `
        -Body    "{`"scope`":`"$RequiredScope`"}" `
        -Headers @{ 'Content-Type' = 'application/json' }
} else {
    $Body = @{
        clientId    = $SpObjectId
        consentType = 'AllPrincipals'
        principalId = $null
        resourceId  = $GraphResourceId
        scope       = $RequiredScope
    } | ConvertTo-Json -Compress
    Invoke-GraphRequest -Method POST `
        -Uri     'https://graph.microsoft.com/v1.0/oauth2PermissionGrants' `
        -Body    $Body `
        -Headers @{ 'Content-Type' = 'application/json' }
}

# ── Summary ──────────────────────────────────────────────────────────────────

$GrantedScopes = @(if ($Grant -and $Grant.scope) {
    $Grant.scope -split ' ' | Sort-Object
})

$FinalUris = az ad app show --id $A365AppId `
    --query 'publicClient.redirectUris' --output json | ConvertFrom-Json

Write-Host ""
Write-Host "===== A365 CLI app registration summary ====="
Write-Host "  App ID            : $A365AppId"
Write-Host "  SP_OBJECT_ID      : $SpObjectId"
Write-Host "  GRAPH_RESOURCE_ID : $GraphResourceId"
Write-Host "  Redirect URIs ($($FinalUris.Count)):"
$FinalUris | ForEach-Object { Write-Host "    - $_" }
Write-Host "  Granted permissions ($($GrantedScopes.Count) / 14):"
$GrantedScopes | ForEach-Object { Write-Host "    + $_" }

if ($GrantedScopes.Count -lt 14) {
    Write-Warning "  Only $($GrantedScopes.Count)/14 permissions in grant response — check for errors above."
    if ($GrantedScopes.Count -eq 13) {
        Write-Warning "  User.ReadWrite.All may require a Global Administrator — re-run as Global Admin if missing."
    }
}

Write-Host ""
Write-Host "  *** DO NOT click 'Grant admin consent' in the Entra admin center ***"
Write-Host "  The portal cannot see beta permissions and will overwrite this grant,"
Write-Host "  deleting the AgentIdentity/AgentInstance scopes. Re-run this script"
Write-Host "  to restore them if accidentally deleted."
Write-Host "============================================="