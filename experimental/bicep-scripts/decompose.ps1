# References for ARM and Graph resource types
# https://learn.microsoft.com/en-us/azure/templates/microsoft.botservice/botservices?pivots=deployment-language-bicep
# https://learn.microsoft.com/en-us/graph/templates/bicep/reference/applications?view=graph-bicep-1.0

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [Alias('g')]
    [string]$RESOURCE_GROUP,

    [Parameter(Mandatory=$true)]
    [Alias('n')]
    [string]$BOT_NAME,

    [string]$APP_ID=''
)

if ($APP_ID -ne '') {
    Write-Output 'Showing App Registration Details:\n'
    az ad app show --id $APP_ID
    Write-Output '\nAssociated federated-credential list:'
    az ad app federated-credential list --id $APP_ID
}

$CHANNEL_NAME_LIST = @('msteams', 'webchat', 'directline')

# Azure Bot Service Channels
Write-Output 'Showing configured channels (from a non-exhaustive list)'
foreach($channel_name in $CHANNEL_NAME_LIST) {
    Write-Output "Channel: $channel_name"
    az bot $CHANNEL_NAME show -n $BOT_NAME -g $RESOURCE_GROUP
    Write-Output '\n'
}

# Azure Bot Service Connections
Write-Output 'Showing connections'
az bot authsetting list -n $BOT_NAME -g $RESOURCE_GROUP
Write-Output '\n'