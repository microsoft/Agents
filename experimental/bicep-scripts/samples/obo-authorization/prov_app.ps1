[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [Alias('g')]
    [string]$RESOURCE_GROUP,

    [Parameter(Mandatory=$true)]
    [Alias('n')]
    [string]$BOT_NAME,

    [Alias('d')]
    [string]$DEPLOYMENT_NAME = 'agent-deployment'
)

$appId = az deployment group create -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME --template-file ../../bicep/simple_app.bicep `
    --parameter botName=$BOT_NAME `
    --query properties.outputs.appId.value --output tsv

Write-Output $appId