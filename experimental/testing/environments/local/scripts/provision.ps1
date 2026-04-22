<#
.SYNOPSIS
    Provisions Azure infrastructure for the LOCAL test environment.

.DESCRIPTION
    Provisions an App Registration, Azure Bot, and Key Vault. Generates a client
    secret, stores it in Key Vault, then writes agent config files (.env /
    appsettings.local.json) for every language variant of the given scenario.

    Agents still run locally as subprocesses (via run_agent.ps1). Only the Azure
    resources needed for Bot Framework auth are provisioned here.

.PARAMETER ResourceGroup
    Azure resource group name (created if it does not exist).

.PARAMETER EnvironmentName
    Short name passed to Bicep as environmentName (max 32 chars). Default: e2e-local.

.PARAMETER Location
    Azure region. Default: eastus.

.PARAMETER Scenario
    Agent scenario folder under environments/local/agents/. Default: quickstart.

.PARAMETER BotEndpoint
    Messaging endpoint registered with the Azure Bot. Use the default for anonymous
    local testing; substitute a devtunnel URL for JWT auth scenarios.

.EXAMPLE
    # From anywhere — script resolves all paths from its own location.
    ./environments/local/scripts/provision.ps1 -ResourceGroup rg-e2e-local
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias('g')]
    [string]$ResourceGroup,

    [Alias('n')]
    [string]$EnvironmentName = 'e2e-local',

    [Alias('l')]
    [string]$Location = 'eastus',

    [string]$Scenario = 'quickstart',

    [string]$BotEndpoint = 'https://localhost:3978/api/messages',

    # Skip Blob Storage and Cosmos DB provisioning (faster for bot-only scenarios).
    [switch]$NoBlobStorage,
    [switch]$NoCosmosDb
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve canonical paths so the script works from any working directory.
$EnvDir    = (Resolve-Path "$PSScriptRoot/..").Path          # environments/local/
$RepoRoot  = (Resolve-Path "$PSScriptRoot/../../..").Path    # repo root
$InfraDir  = Join-Path $EnvDir 'infra'
$OutputsFile = Join-Path $EnvDir 'infra-outputs.json'
$DeploymentName = "e2e-local-$EnvironmentName"

# --- 1. Ensure resource group ---
Write-Host "Ensuring resource group '$ResourceGroup' in '$Location'..."
az group create --name $ResourceGroup --location $Location --output none

# --- 2. Get deployer principal ID (Key Vault Secrets Officer assignment) ---
$DeployerPrincipalId = az ad signed-in-user show --query id --output tsv 2>$null
if (-not $DeployerPrincipalId) {
    $DeployerPrincipalId = az account show --query user.name --output tsv
}

# --- 3. Deploy Bicep ---
Write-Host "Deploying $InfraDir/main.bicep..."

$BicepParams = @(
    '--resource-group', $ResourceGroup,
    '--name', $DeploymentName,
    '--template-file', "$InfraDir/main.bicep",
    '--parameters',
        "environmentName=$EnvironmentName",
        "location=$Location",
        "botEndpoint=$BotEndpoint",
        "deployerPrincipalId=$DeployerPrincipalId",
        "deployBlobStorage=$(if ($NoBlobStorage) { 'false' } else { 'true' })",
        "deployCosmosDb=$(if ($NoCosmosDb) { 'false' } else { 'true' })",
    '--query', 'properties.outputs',
    '--output', 'json'
)

$RawOutputs = az @BicepParams | ConvertFrom-Json

if (-not $RawOutputs) {
    Write-Error "Deployment failed or returned no outputs."
    exit 1
}

# Flatten {KEY: {value: "..."}} -> {KEY: "..."}
$Outputs = @{}
foreach ($prop in $RawOutputs.PSObject.Properties) {
    $Outputs[$prop.Name] = $prop.Value.value
}

$Outputs | ConvertTo-Json | Out-File $OutputsFile -Encoding utf8
Write-Host "Infra outputs written to $OutputsFile"

# --- 4. Generate client secret and store in Key Vault ---
Write-Host "Generating client secret for app '$($Outputs.APP_ID)'..."
$ClientSecret = az ad app credential reset `
    --id $Outputs.APP_ID `
    --query password `
    --output tsv

Write-Host "Storing client secret in Key Vault '$($Outputs.KEY_VAULT_NAME)'..."
az keyvault secret set `
    --vault-name $Outputs.KEY_VAULT_NAME `
    --name 'client-secret' `
    --value $ClientSecret `
    --output none

# --- 5. Inject credentials into agent config files ---
Write-Host "Injecting config into agent source files..."

# inject_config.py resolves environments/local/agents/ relative to CWD — run from repo root.
Push-Location $RepoRoot
try {
    $VarArgs = '--var', "AUTH_TYPE=ClientSecret", '--var', "CLIENT_SECRET=$ClientSecret", '--var', "UMI_CLIENT_ID="
    uv run python "$PSScriptRoot/inject_config.py" `
        --scenario $Scenario `
        --outputs-file $OutputsFile `
        @VarArgs
} finally {
    Pop-Location
}

Write-Host "Provisioning complete."
