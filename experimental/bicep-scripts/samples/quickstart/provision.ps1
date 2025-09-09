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

    [Alias('l')]
    [string]$LOCATION = 'global',

    [Alias('d')]
    [string]$DEPLOYMENT_NAME = 'agent-deployment'
)

$appId = az deployment group create -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME --template-file ../../bicep/simple_app.bicep `
    --parameter botName=$BOT_NAME `
    --query properties.outputs.appId.value --output tsv

az deployment group create -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME --template-file ../../bicep/bot.bicep `
    --parameter appId=$appId `
    --parameter endpoint=$ENDPOINT `
    --parameter location=$LOCATION `
    --parameter botName=$BOT_NAME `
    --query properties.outputs.appId.value --output tsv

$appSecret = az ad app credential reset --id $appId --query password --output tsv

Write-Output "App ID:"
Write-Output $appId

Write-Output "App Secret:"
Write-Output $appSecret