<#
.SYNOPSIS
Creates an Azure Bot

.DESCRIPTION
This creates an Azure Bot in the indicated resource group and auth type.

.PARAMETER AuthType
UserManagedIdentity | ClientSecret | FederatedCredentials

.PARAMETER ResourceGroup
The name of the resource group where the Azure Bot is located.

.PARAMETER AzureBotName
The name of the Azure Bot.

.EXAMPLE
create-azurebot -ResourceGroup myResourceGroup -AzureBotName myAzureBot -AuthType FederatedCredentials
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$AuthType,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AzureBotName,

    [switch]$Teams
)

. ./func-create-fic.ps1
. ./func-create-secret.ps1
. ./func-create-msi.ps1

# Formats JSON in a nicer format than the built-in ConvertTo-Json does.
function Format-Json([Parameter(Mandatory, ValueFromPipeline)][String] $json) {
    $indent = 0;
    ($json -Split "`n" | % {
        if ($_ -match '[\}\]]\s*,?\s*$') {
            # This line ends with ] or }, decrement the indentation level
            $indent--
        }
        $line = ('  ' * $indent) + $($_.TrimStart() -replace '":  (["{[])', '": $1' -replace ':  ', ': ')
        if ($_ -match '[\{\[]\s*$') {
            # This line ends with [ or {, increment the indentation level
            $indent++
        }
        $line
    }) -Join "`n"
}


# Create identity
try {
    if ($AuthType -eq "UserManagedIdentity") {
        $createResult = Create-Agent-UserManagedIdentity -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName | ConvertFrom-Json
    } elseif ($AuthType -eq "ClientSecret") {
        $createResult = Create-Agent-ClientSecret -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName | ConvertFrom-Json
    } elseif ($AuthType -eq "FederatedCredentials") {
        $createResult = Create-Agent-FederatedCredentials -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName | ConvertFrom-Json
    } else {
        Write-Error "Unsupported authentication type: $AuthType"
        exit 1
    }
} catch {
    Write-Error "Failed to create Agent identity: $_"
    exit 1
}

$config = $createResult.Config | ConvertTo-Json -Depth 10 | Format-Json
Write-Host "`nAgent Configuration:`n`n$config`n"

try {
    # Create Azure Bot
    if ($AuthType -eq "UserManagedIdentity") {
        $botCreateResult = az bot create --app-type $createResult.AzureBotAppType --appid $createResult.ClientId --msi-resource-id $createResult.ResourceId --resource-group $ResourceGroup --name $AzureBotName --tenant-id $createResult.TenantId | ConvertFrom-Json
    } else {
        $botCreateResult = az bot create --app-type $createResult.AzureBotAppType --appid $createResult.ClientId --resource-group $ResourceGroup --name $AzureBotName --tenant-id $createResult.TenantId | ConvertFrom-Json
    }

    Write-Host "Created Azure Bot: $($botCreateResult.resourceGroup)/$($botCreateResult.name)"

    if ($Teams) {
        az bot msteams create --name $AzureBotName --resource-group $ResourceGroup | out-null
    }
} catch {
    # TODO: delete identity
    Write-Error "Failed to create Azure Bot: $_"
}
