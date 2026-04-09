<#
.SYNOPSIS
    azd predown hook for the LOCAL environment.

.DESCRIPTION
    Runs before `azd down` deletes the resource group.

    Azure Bot Service and Key Vault are ARM resources — they are deleted when
    the resource group is removed. The App Registration and its Service Principal
    live in Microsoft Entra (outside the resource group) and must be deleted here
    or they will be orphaned.

    APP_ID is read from the azd environment, which is populated from the Bicep
    output written during `azd provision`.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AppId = azd env get-value APP_ID 2>$null
if (-not $AppId) {
    Write-Warning "APP_ID not found in azd environment — skipping App Registration cleanup."
    exit 0
}

Write-Host "Deleting App Registration '$AppId' (and its Service Principal)..."
az ad app delete --id $AppId
Write-Host "App Registration deleted."
