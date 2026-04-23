<#
.SYNOPSIS
    azd postprovision hook

.DESCRIPTION
    azd populates Bicep outputs as environment variables before running hooks.
    This script retrieves (or generates) the client secret, writes everything to
    .env, then calls inject_config.py to populate agent config files.

    Run automatically by `azd provision` (from environment directory containing azure.yaml).
    Can also be run manually after `azd provision` completes (reads existing
    .env when azd env vars are not set).
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir     = (Resolve-Path "$PSScriptRoot/..").Path
$DotEnvFile = Join-Path $EnvDir '.env'

# azd populates these from Bicep outputs when running as a hook.
# When run manually, fall back to the existing .env.
$Outputs = @{
    APP_ID         = $env:APP_ID
    TENANT_ID      = $env:TENANT_ID
    KEY_VAULT_NAME = $env:KEY_VAULT_NAME
    KEY_VAULT_URI  = $env:KEY_VAULT_URI
    BOT_NAME       = $env:BOT_NAME
}

$missing = $Outputs.GetEnumerator() | Where-Object { -not $_.Value }
if ($missing) {
    if (-not (Test-Path $DotEnvFile)) {
        Write-Error "Required azd outputs are not set and $DotEnvFile does not exist. Run 'azd provision' from environments/local/ first."
        exit 1
    }
    Write-Host "azd env vars not set - loading infra outputs from $DotEnvFile"
    $Outputs = @{}
    foreach ($line in Get-Content $DotEnvFile) {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line -match '^([^=]+)=(.*)$') {
            $Outputs[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
} else {
    # Optional outputs — use $null for undeployed modules so they are omitted from .env.
    $Outputs.STORAGE_ACCOUNT_URL = if ($env:STORAGE_ACCOUNT_URL) { $env:STORAGE_ACCOUNT_URL } else { $null }
    $Outputs.BLOB_CONTAINER_NAME = if ($env:BLOB_CONTAINER_NAME) { $env:BLOB_CONTAINER_NAME } else { $null }
    $Outputs.COSMOS_DB_ENDPOINT  = if ($env:COSMOS_DB_ENDPOINT)  { $env:COSMOS_DB_ENDPOINT }  else { $null }
    $Outputs.COSMOS_DB_DATABASE  = if ($env:COSMOS_DB_DATABASE)  { $env:COSMOS_DB_DATABASE }  else { $null }
    $Outputs.COSMOS_DB_CONTAINER = if ($env:COSMOS_DB_CONTAINER) { $env:COSMOS_DB_CONTAINER } else { $null }
}

# Retrieve existing client secret from Key Vault, or generate a new one on first run.
$ClientSecret = az keyvault secret show `
    --vault-name $Outputs.KEY_VAULT_NAME `
    --name 'client-secret' `
    --query value --output tsv 2>$null

if (-not $ClientSecret) {
    Write-Host "No client secret found in Key Vault - generating one for app '$($Outputs.APP_ID)'..."
    $ClientSecret = az ad app credential reset `
        --id $Outputs.APP_ID `
        --query password `
        --output tsv

    az keyvault secret set `
        --vault-name $Outputs.KEY_VAULT_NAME `
        --name 'client-secret' `
        --value $ClientSecret `
        --output none
    Write-Host "Client secret stored in Key Vault '$($Outputs.KEY_VAULT_NAME)'."
}

# Add auth values and write the complete .env (including secret).
$Outputs.AUTH_TYPE     = 'ClientSecret'
$Outputs.CLIENT_SECRET = $ClientSecret
$Outputs.UMI_CLIENT_ID = ''

$envLines = $Outputs.GetEnumerator() | Where-Object { $null -ne $_.Value } | Sort-Object Key |
    ForEach-Object { "$($_.Key)=$($_.Value)" }
$envLines | Out-File $DotEnvFile -Encoding utf8
Write-Host "Outputs written to $DotEnvFile"

Write-Host "Injecting config..."
Push-Location $EnvDir
try {
    uv run --no-project python "$PSScriptRoot/inject_config.py" --vars-file $DotEnvFile
} finally {
    Pop-Location
}
