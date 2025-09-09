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
    
    [ValidateSet('aadv2')]
    [string]$OAUTH_TYPE = 'aadv2',

    [Alias('l')]
    [string]$LOCATION = 'global',

    [Alias('d')]
    [string]$DEPLOYMENT_NAME = 'agent-deployment'
)

$appId = ./prov_app.ps1 `
    -g $RESOURCE_GROUP `
    -n $BOT_NAME `
    -e $ENDPOINT `
    -d $DEPLOYMENT_NAME

./prov_bot.ps1 `
    -g $RESOURCE_GROUP `
    -n $BOT_NAME `
    -e $ENDPOINT `
    -l $LOCATION `
    -d $DEPLOYMENT_NAME `
    -APP_ID $appId `
    
$appSecret = az ad app credential reset --id $appId --query password --output tsv

echo "App ID:"
echo $appId

echo "App Secret:"
echo $appSecret