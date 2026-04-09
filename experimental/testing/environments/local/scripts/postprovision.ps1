<#
.SYNOPSIS
    azd postprovision hook for the LOCAL environment.

.DESCRIPTION
    azd populates Bicep outputs as environment variables before running hooks.
    This script writes them to .infra.json then calls inject_config.py to
    populate agent config files.

    Run automatically by `azd provision` (from environments/local/).
    Can also be run manually after `azd provision` completes (reads existing
    .infra.json when azd env vars are not set).
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EnvDir      = (Resolve-Path "$PSScriptRoot/..").Path
$OutputsFile = Join-Path $EnvDir '.infra.json'

# azd populates these from Bicep outputs when running as a hook.
# When run manually, fall back to the existing .infra.json.
$Outputs = @{
    APP_ID         = $env:APP_ID
    TENANT_ID      = $env:TENANT_ID
    KEY_VAULT_NAME = $env:KEY_VAULT_NAME
    KEY_VAULT_URI  = $env:KEY_VAULT_URI
    BOT_NAME       = $env:BOT_NAME
}

$missing = $Outputs.GetEnumerator() | Where-Object { -not $_.Value }
if ($missing) {
    if (-not (Test-Path $OutputsFile)) {
        Write-Error "Required azd outputs are not set and $OutputsFile does not exist. Run 'azd provision' from environments/local/ first."
        exit 1
    }
    Write-Host "azd env vars not set - loading infra outputs from $OutputsFile"
    $json = Get-Content $OutputsFile -Raw | ConvertFrom-Json
    $Outputs = @{}
    $json.PSObject.Properties | ForEach-Object { $Outputs[$_.Name] = $_.Value }
} else {
    $Outputs | ConvertTo-Json | Out-File $OutputsFile -Encoding utf8
    Write-Host "Infra outputs written to $OutputsFile"
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

$Scenario = $env:AGENT_SCENARIO
if (-not $Scenario) {
    Write-Warning "AGENT_SCENARIO environment variable not set; defaulting to 'quickstart'"
    $Scenario = 'quickstart'
}

Write-Host "Injecting config (scenario=$Scenario)..."
Push-Location $EnvDir
try {
    $VarArgs = '--var', "AUTH_TYPE=ClientSecret", '--var', "CLIENT_SECRET=$ClientSecret", '--var', "UMI_CLIENT_ID="
    $UvArgs = @('run', 'python', "$PSScriptRoot/inject_config.py",
                '--scenario', $Scenario,
                '--outputs-file', $OutputsFile) + $VarArgs
    uv @UvArgs
} finally {
    Pop-Location
}
