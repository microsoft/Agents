Function Create-Agent-UserManagedIdentity {
    <#
    .SYNOPSIS
    Creates a User Managed Identity Credential for an Azure Bot.

    .DESCRIPTION
    This function creates a User Managed Identity Credential for an Azure Bot using the Azure CLI.
    
    .PARAMETER ResourceGroup
    The name of the resource group where the Azure Bot is located.

    .PARAMETER AzureBotName
    The name of the Azure Bot for which the User Managed Identity Credential is created.

    .EXAMPLE
    Create-Agent-UserManagedIdentity -ResourceGroup "myResourceGroup" -AzureBotName "myAzureBot"
    #>
    
    [CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AzureBotName
)

$identityCreateResult = az identity create --resource-group "$ResourceGroup" --name "$AzureBotName" --output json | ConvertFrom-Json

return @"
{
  "ClientId": "$($identityCreateResult.clientId)",
  "TenantId": "$($identityCreateResult.tenantId)",
  "ResourceId": "$($identityCreateResult.id)",
  "AzureBotAppType": "UserAssignedMSI",
  "Config": {
    "dotnet": {
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "UserManagedIdentity",
            "ClientId": "$($identityCreateResult.clientId)",
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