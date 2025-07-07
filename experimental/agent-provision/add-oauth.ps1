<#
.SYNOPSIS
Creates an OAuth Connection on an Azure Bot

.DESCRIPTION
This udpates the required App Registration settings for OAuth, and creates an OAuth Connection on the Azure Bot.

.PARAMETER AuthType
Aadv2WithFic

.PARAMETER ResourceGroup
The name of the resource group where the Azure Bot is located.

.PARAMETER AzureBotName
The name of the Azure Bot.

.PARAMETER TeamsSSO
Specify to setup for Teams SSO.  Default is false.

.EXAMPLE
add-oauth -ResourceGroup myResourceGroup -AzureBotName myAzureBot -AuthType Aadv2WithFic
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$AuthType,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AzureBotName,

    [string]$ConnectionName = "oauth",

    [string]$RedirectUri = "https://token.botframework.com/.auth/web/redirect",

    [switch]$TeamsSSO,

    [switch]$ExchangeableToken
)

if ($AuthType -ne "Aadv2WithFic") {
    Write-Error "Unsupported authentication type: $AuthType"
    exit 1
}

function Convert-Tenant-To-Base64Url {
    param (
        [Parameter(Mandatory=$true)]
        [string]$TenantId
    )

    $bytes = [guid]::Parse($TenantId).ToByteArray()
    $base64 = [System.Convert]::ToBase64String($bytes)
    $base64Url = $base64.TrimEnd('=') -replace '\+', '-' -replace '/', '_'
    return $base64Url
}

$tempFile = "oauth-temp.json"

try {
    # What is the bot?
    $botInfo = az bot show --name $AzureBotName --resource-group $ResourceGroup | ConvertFrom-Json
    $botProperties = $botInfo.properties

    $tenantId = $botProperties.msaAppTenantId  # Not sure if this set for multi-tenant
    $botAppId = $botProperties.msaAppId
    $isManaged = $null -ne $botProperties.msaAppMsiResourceId -and $botProperties.msaAppMsiResourceId

    # Create/Reuse App Registration
    if ($isManaged) {
        # If the Azure Bot is using Managed Identity, we need to create an App Registration for OAuth.

        if ($TeamsSSO) {
            # TBD:  Should probably confirm this to proceed.
            Write-Host "This is a Managed Identity Azure Bot.  Teams SSO requires an App Registration.  Setting up for OAuth flow instead."
        }

        # Get identity info
        $identity = az identity show --id @($botProperties.msaAppMsiResourceId) | ConvertFrom-Json

        # Create new App Registration for OAuth
        $OAuthAppRegName = "$AzureBotName-$ConnectionName"
        $appCreateResult = az ad app create --display-name $OAuthAppRegName --sign-in-audience "AzureADMyOrg" | ConvertFrom-Json

        $federatedCredential = '{\"name\": \"agent\", \"issuer\": \"https://login.microsoftonline.com/' + $identity.tenantId + '/v2.0\", \"subject\": \"' + $identity.principalId + '\",\"audiences\": [\"api://AzureADTokenExchange\"]}' 
        az ad app federated-credential create --id $appCreateResult.appId --parameters $federatedCredential | ConvertFrom-Json  | out-null

        $OAuthAppId = $appCreateResult.appId
        if ($ConnectionName -eq "oauth") {
            $ConnectionName = "teams_oauth"
        }            
    } else {
        if ($TeamsSSO) {
            # Teams SSO uses the same App Reg as the Azure Bot
            $OAuthAppRegName = $AzureBotName
            $OAuthAppId = $botAppId
            if ($ConnectionName -eq "oauth") {
                $ConnectionName = "teams_sso"
            }            
            Write-Host "Use existing App Registration: '$OAuthAppRegName'"
        } else {
            # create a new App Reg for the OAuth Connection
            $OAuthAppRegName = "$AzureBotName-$ConnectionName"
            Write-Host "Create App Registration: '$OAuthAppRegName'"
            $appCreateResult = az ad app create --display-name $OAuthAppRegName --sign-in-audience "AzureADMyOrg" | ConvertFrom-Json
            $OAuthAppId = $appCreateResult.appId
        }
    }

    # Expose an API
    Write-Host "  Exposing API"

    $identifierUri = "api://botid-$OAuthAppId"
    $apiScopeId = [guid]::NewGuid().Guid

    $apiScope = @{
        oauth2PermissionScopes = @(
            @{
                adminConsentDisplayName = "Admin consent"
                adminConsentDescription = "Admin consent for $AzureBotName on $ResourceGroup"
                id = $apiScopeId
                isEnabled = $true
                type = "User"
                userConsentDisplayName = "User consent"
                userConsentDescription = "User consent for $AzureBotName on $ResourceGroup"
                value = "defaultScope"
            }
        )
        requestedAccessTokenVersion = 2
    }

    # writing json to temp file because of #fail to figure out quote escaping
    $apiScope | ConvertTo-Json -Depth 4 -Compress | Out-File -FilePath $tempFile

    # This will fail if one already exists
    az ad app update --id $OAuthAppId --identifier-uris $identifierUri --web-redirect-uris $RedirectUri --set api=@$($tempFile)

    if ($TeamsSSO) {
        Write-Host "  Adding default Teams clients"
        # Add Teams web and desktop clients
        $teamsClients = @{
            preAuthorizedApplications = @(
                @{
                    appId  = "1fec8e78-bce4-4aaf-ab1b-5451cc387264"
                    delegatedPermissionIds = @($apiScopeId)
                }
                @{
                    appId = "5e3ce6c0-2b1f-4285-8d4b-75ee78787346"
                    delegatedPermissionIds = @($apiScopeId)
                }
            )
        }

        # writing json to temp file because of #fail to figure out quote escaping
        $teamsClients | ConvertTo-Json -Depth 4 -Compress | Out-File -FilePath $tempFile
        
        az ad app update --id $OAuthAppId --identifier-uris $identifierUri --web-redirect-uris $redirectUri --set api=@$($tempFile)
    }

    # Add default API permissions (User.Read)
    Write-Host "  Adding default API Permissions"
    $requiredAccess = '[{\"resourceAppId\": \"00000003-0000-0000-c000-000000000000\", \"resourceAccess\": [{\"id\": \"e1fe6dd8-ba31-4d61-89e7-88639da4683d\", \"type\": \"Scope\"}]}]'
    az ad app update --id $OAuthAppId --required-resource-accesses $requiredAccess

    # Add FIC OAuth credential to App Reg
    Write-Host "  Creating OAuth Federated Credential"
    $encodedTenant = Convert-Tenant-To-Base64Url -TenantId $tenantId
    $uniqueIdentifier = "$AzureBotName-$ConnectionName"
    $federatedCredential = '{\"name\": \"agent-oauth\", \"issuer\": \"https://login.microsoftonline.com/' + $tenantId + '/v2.0\", \"subject\": \"/eid1/c/pub/t/' + $encodedTenant + '/a/9ExAW52n_ky4ZiS_jhpJIQ/' + $uniqueIdentifier +'\",\"audiences\": [\"api://AzureADTokenExchange\"]}' 
    $ficResult = az ad app federated-credential create --id $OAuthAppId --parameters $federatedCredential | ConvertFrom-Json

    # Create OAuth connection
    Write-Host "Creating OAuth Connection '$ConnectionName' on '$AzureBotName'"

    if ($ExchangeableToken) {
        # TODO: probably need to set the Access Tokens/ID Token above
        $scopes = "$identifierUri/defaultScope"
    } else {
        $scopes = "User.Read"
    }

    az bot authsetting create `
        --name $AzureBotName `
        --resource-group $ResourceGroup `
        --setting-name $ConnectionName `
        --client-id $OAuthAppId `
        --client-secret "unused-but-required" `
        --service Aadv2WithFic `
        --provider-scope-string $scopes `
        --parameters TenantId=$tenantId TokenExchangeUrl=$identifierUri UniqueIdentifier=$uniqueIdentifier | out-null
}
catch {
    Write-Error "Failed to create OAuth Connection: $_"
}
finally {
    if (Test-Path $tempFile)
    {
        Remove-Item -Path $tempFile -Force
    }
}
