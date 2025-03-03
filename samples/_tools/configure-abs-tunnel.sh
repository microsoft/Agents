#!/bin/bash

resource_group=$HOSTNAME-dev
tunnel_id=$HOSTNAME-tunnel

if ! az account show &> /dev/null; then
    echo "Please login to Azure CLI."
    az login
fi

if ! devtunnel list &> /dev/null; then
    echo "Please login to devtunnel."
    devtunnel user login
fi

if ! devtunnel show $tunnel_id &> /dev/null; then
    echo "Creating tunnel: $tunnel_id in port 3978"
    devtunnel create $tunnel_id -a
    devtunnel port create $tunnel_id -p 3978
fi

if ! az group show --name $resource_group &> /dev/null; then
    echo "Creating resource group: $resource_group"
    az group create --name $resource_group --location eastus
fi

tunnel_details=$(devtunnel show $tunnel_id -j -v)
tunnel_url=$(echo $tunnel_details | grep -oP '(?<="portForwardingUris": \[ ")[^"]+(?=" \])')
echo $tunnel_url

appId=$(az ad app create --display-name $tunnel_id --sign-in-audience "AzureADMyOrg" --query appId -o tsv)
echo "Created AppId: "  $appId
secretJson=$(az ad app credential reset --id $appId | jq .)

clientId=$(echo $secretJson | jq .appId | tr -d '"')
tenantId=$(echo $secretJson | jq .tenant | tr -d '"')
clientSecret=$(echo $secretJson | jq .password | tr -d '"')

echo "clientId=$clientId" > "$tunnel_id.env"
echo "tenantId=$tenantId" >> "$tunnel_id.env"
echo "clientSecret=$clientSecret" >> "$tunnel_id.env"
echo "DEBUG=agents:*.info" >> "$tunnel_id.env"

echo "Env File created: $tunnel_id.env"

endpoint=$tunnel_url"api/messages"

botJson=$(az bot create \
    --app-type SingleTenant \
    --appid $appId \
    --tenant-id $tenantId \
    --name $tunnel_id \
    --resource-group $resource_group \
    --endpoint $endpoint)

echo $botJson
$teamsBotJson=$(az bot msteams create -n $tunnel_id -g $resource_group)
echo $teamsBotJson

echo "Bot created: $tunnel_id"