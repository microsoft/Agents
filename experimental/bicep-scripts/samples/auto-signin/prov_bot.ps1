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
    [string]$DEPLOYMENT_NAME = 'agent-deployment',

    [Parameter(Mandatory=$true)]
    [string]$APP_ID
)

az deployment group create -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME --template-file ../../bicep/bot.bicep `
    --parameter endpoint=$ENDPOINT `
    --parameter location=$LOCATION `
    --parameter botName=$BOT_NAME `
    --parameter appId=$APP_ID

az deployment group create -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME --template-file ./oauth.bicep `
    --parameter botName=$BOT_NAME `
    --parameter appId=$APP_ID `
    --parameter endpoint=$ENDPOINT `
    --parameter location=$LOCATION