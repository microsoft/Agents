## This script will create a Bot Service instance, using Azure Container Apps and Azure Bot Service
## Variables can be configured as EnvVars, if not found should be provided by the user

function Get-EnvOrPrompt {
    param (
        [string]$envVarName,
        [string]$promptMessage
    )

    $envVarValue = [System.Environment]::GetEnvironmentVariable($envVarName)
    if (-not $envVarValue) {
        $envVarValue = Read-Host $promptMessage
    }
    return $envVarValue
}

# Initialize variables
$resourceGroup = Get-EnvOrPrompt -envVarName "RESOURCE_GROUP" -promptMessage "Enter the name of the resource group, eg my-test-rg, it has to exist"
$acrName = Get-EnvOrPrompt -envVarName "ACR_NAME" -promptMessage "Enter the name of the ACR, eg my-test-acr, it has to exist"
$acrImage = Get-EnvOrPrompt -envVarName "ACR_IMAGE" -promptMessage "Enter the name of the image, eg my-test-bot:latest, it has to exist in the ACR $acrName" 
$botName = Get-EnvOrPrompt -envVarName "BOT_NAME" -promptMessage "Enter the name of the bot, eg my-test-bot"

$containerName = "$botName-app"
$botImage = "$acrName.azurecr.io/$acrImage"

echo "Creating Bot $botName from image $botImage in $containerName, RG $resourceGroup"

## Create the AppId and Secret
$appId = az ad app create --display-name $botName --sign-in-audience "AzureADMyOrg" --query appId | ConvertFrom-Json
echo "Created AppId: "  $appId
$secretJson = az ad app credential reset --id $appId | ConvertFrom-Json

## Create the Azure Container App
az containerapp up `
    --resource-group $resourceGroup `
    --name $containerName `
    --image $botImage `
    --environment bot-apps-$resourceGroup `
    --ingress external `
    --env-vars `
        tenantId=$($secretJson.tenant) `
        DEBUG="*"

$fqdn = az containerapp show --resource-g  $resourceGroup  --name $containerName --query properties.configuration.ingress.fqdn -o tsv
$endpoint = "https://$fqdn/api/messages"
echo "Created ACA app, listenting in endpoint: $endpoint"

## Create the Bot Service
$botJson = az bot create `
    --app-type UserAssignedMSI `
    --appid $appId `
    --tenant-id $($secretJson.tenant) `
    --name $botName `
    --resource-group $resourceGroup `
    --endpoint $endpoint

$teamsBotJson = az bot msteams create -n $botName -g $resourceGroup
echo $teamsBotJson