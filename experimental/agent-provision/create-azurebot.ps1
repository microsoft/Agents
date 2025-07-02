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

.PARAMETER Teams
Indicates this is a Teams Agent

.EXAMPLE
create-azurebot -ResourceGroup myResourceGroup -AzureBotName myAzureBot -AuthType FederatedCredentials
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("FederatedCredentials", "UserManagedIdentity", "ClientSecret")]
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
        $createResult = Create-Agent-UserManagedIdentity -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName
    } elseif ($AuthType -eq "ClientSecret") {
        $createResult = Create-Agent-ClientSecret -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName
    } elseif ($AuthType -eq "FederatedCredentials") {
        $createResult = Create-Agent-FederatedCredentials -ResourceGroup $ResourceGroup -AzureBotName $AzureBotName
    } else {
        Write-Error "Unsupported authentication type: $AuthType"
        exit 1
    }
} catch {
    Write-Error "Failed to create Agent identity: $_"
    exit 1
}

try {
    # Create Azure Bot
    if ($createResult.AzureBotAppType -eq "UserAssignedMSI") {
        az bot create --app-type $createResult.AzureBotAppType --appid $createResult.ClientId --msi-resource-id $createResult.ResourceId --resource-group $ResourceGroup --name $AzureBotName --tenant-id $createResult.TenantId 2>&1 | Out-Null
    } else {
        az bot create --app-type $createResult.AzureBotAppType --appid $createResult.ClientId --resource-group $ResourceGroup --name $AzureBotName --tenant-id $createResult.TenantId 2>&1 | Out-Null
    }

    if($LASTEXITCODE){
        Throw $Error[0]
    }

    if ($Teams) {
        az bot msteams create --name $AzureBotName --resource-group $ResourceGroup --only-show-errors | out-null
    }

    return $createResult  | ConvertTo-Json -Depth 10 | Format-Json
} catch {
    Write-Error "Failed to create Azure Bot: $_"

    # Remove resources
    if ($AuthType -eq "FederatedCredentials") {
        Remove-Agent-FederatedCredentials -CreateResult $createResult
    } elseif ($AuthType -eq "UserManagedIdentity") {
        Remove-Agent-UserManagedIdentity -CreateResult $createResult
    } elseif ($AuthType -eq "ClientSecret") {
        Remove-Agent-ClientSecret -CreateResult $createResult
    }
    exit 2
}
