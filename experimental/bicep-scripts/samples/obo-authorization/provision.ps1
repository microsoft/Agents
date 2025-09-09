[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [Alias('g')]
    [string]$RESOURCE_GROUP,

    [Parameter(Mandatory=$true)]
    [Alias('n')]
    [string]$BOT_NAME,

    [Parameter(Mandatory=$true)]
    [Alias('e')]
    [string]$ENDPOINT,

    [ValidateSet('aadv2', 'none')]
    [string]$OAUTH_TYPE = 'aadv2',

    [Alias('l')]
    [string]$LOCATION = 'global',

    [Alias('d')]
    [string]$DEPLOYMENT_NAME = 'agent-deployment'
)

$appId = ./prov_app.ps1 -g $RESOURCE_GROUP -n $BOT_NAME -d $DEPLOYMENT_NAME 

$oauthAppId = ./prov_oauth_app.ps1 `
    -g $RESOURCE_GROUP `
    -d $DEPLOYMENT_NAME `
    -n $BOT_NAME `
    -e $ENDPOINT `
    -OAUTH_TYPE $OAUTH_TYPE

./prov_bot.ps1 `
    -g $RESOURCE_GROUP `
    -n $BOT_NAME `
    -e $ENDPOINT `
    -l $LOCATION `
    -d $DEPLOYMENT_NAME `
    -APP_ID $appId `
    -OAUTH_APP_ID $oauthAppId `
    -OAUTH_TYPE $OAUTH_TYPE

$appSecret = az ad app credential reset --id $appId --query password --output tsv
$oauthAppSecret = az ad app credential reset --id $oauthAppId --query password --output tsv

Write-Output "Bot App Registration:"
Write-Output "App ID:"
Write-Output $appId

Write-Output "App Secret:"
Write-Output $appSecret

Write-Output "OAuth App Registration:"

Write-Output "App ID:"
Write-Output $oauthAppId

Write-Output "App Secret:"
Write-Output $oauthAppSecret