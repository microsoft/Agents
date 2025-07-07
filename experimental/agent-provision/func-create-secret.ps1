Function Create-Agent-ClientSecret {
    <#
    .SYNOPSIS
    Creates a ClientSecret Identity Credential for an Azure Bot.

    .DESCRIPTION
    This function creates a ClientSecret Identity Credential for an Azure Bot using the Azure CLI.
    
    .PARAMETER ResourceGroup
    The name of the resource group where the Azure Bot is located.

    .PARAMETER AzureBotName
    The name of the Azure Bot for which the ClientSecret Identity Credential is created.

    .EXAMPLE
    Create-Agent-ClientSecret -ResourceGroup "myResourceGroup" -AzureBotName "myAzureBot"
    #>
    
    [CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AzureBotName
)

$appCreateResult = az ad app create --display-name "$AzureBotName" --sign-in-audience "AzureADMyOrg" | ConvertFrom-Json
$appSecret = az ad app credential reset --id $appCreateResult.appId --only-show-errors | ConvertFrom-Json
$tenantId = az account list --query "[?isDefault].tenantId | [0]" --only-show-errors --output tsv

az ad sp create --id $($appCreateResult.appId) | out-null

return @"
{
  "ClientId": "$($appCreateResult.appId)",
  "TenantId": "$($tenantId)",
  "AzureBotAppType": "SingleTenant",
  "Config": {
    "dotnet": {
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret",
            "AuthorityEndpoint": "https://login.microsoftonline.com/$($tenantId)",
            "ClientId": "$($appCreateResult.appId)",
            "ClientSecret": "$($appSecret.password)",
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      }
    },
    "js": null,
    "python": null
  }
}
"@
}