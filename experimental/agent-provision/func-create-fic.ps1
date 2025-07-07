Function Create-Agent-FederatedCredentials {
    <#
    .SYNOPSIS
    Creates a Federated Identity Credential for an Azure Bot.

    .DESCRIPTION
    This function creates a Federated Identity Credential for an Azure Bot using the Azure CLI.
    
    .PARAMETER ResourceGroup
    The name of the resource group where the Azure Bot is located.

    .PARAMETER AzureBotName
    The name of the Azure Bot for which the Federated Identity Credential is created.

    .EXAMPLE
    Create-Agent-FederatedCredentials -ResourceGroup "myResourceGroup" -AzureBotName "myAzureBot"
    #>
    
    [CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AzureBotName
)

$identityCreateResult = az identity create --resource-group "$ResourceGroup" --name "$AzureBotName" --output json | ConvertFrom-Json
$appCreateResult = az ad app create --display-name "$AzureBotName" --sign-in-audience "AzureADMyOrg" | ConvertFrom-Json

$federatedCredential = '{\"name\": \"agent\", \"description\": \"Agent-to-Channel\", \"issuer\": \"https://login.microsoftonline.com/' + $identityCreateResult.tenantId + '/v2.0\", \"subject\": \"' + $identityCreateResult.principalId + '\",\"audiences\": [\"api://AzureADTokenExchange\"]}' 
$ficResult = az ad app federated-credential create --id $appCreateResult.appId --parameters $federatedCredential | ConvertFrom-Json

az ad sp create --id $($appCreateResult.appId) | out-null

return @"
{
  "ClientId": "$($appCreateResult.appId)",
  "TenantId": "$($identityCreateResult.tenantId)",
  "AzureBotAppType": "SingleTenant",
  "Config": {
    "dotnet": {
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "FederatedCredentials",
            "AuthorityEndpoint": "https://login.microsoftonline.com/$($identityCreateResult.tenantId)",
            "ClientId": "$($appCreateResult.appId)",
            "FederatedClientId": "$($identityCreateResult.clientId)", 
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