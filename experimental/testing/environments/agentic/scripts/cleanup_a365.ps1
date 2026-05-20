# <#
# .SYNOPSIS
#     Removes the A365 agent instance and blueprint from Entra ID.

# .DESCRIPTION
#     Runs the A365 CLI cleanup commands that are the inverse of setup_a365.ps1:

#       1. a365 cleanup instance   — deletes the agent identity service principal
#                                    and the Azure AD agent user account.
#       2. a365 cleanup blueprint  — deletes the Entra ID blueprint application
#                                    and its service principal.

#     Instance cleanup runs first because the agent identity lives under the
#     blueprint — deleting the blueprint first would orphan the instance.

#     Failures are logged as warnings rather than thrown so that `azd down` can
#     proceed even when resources have already been deleted out-of-band.

#     Run automatically by predown.ps1 before `azd down`.
#     Can also be run manually from environments/agentic/:
#         .\scripts\cleanup_a365.ps1
# #>
# Set-StrictMode -Version Latest
# $ErrorActionPreference = 'Stop'

# $EnvDir    = (Resolve-Path "$PSScriptRoot/..").Path
# $AgentName = 'agents-e2e-agentic-agent'
# Push-Location $EnvDir

# try {
#     Write-Host "Cleaning up A365 agent instance..."
#     a365 cleanup instance --verbose --yes --agent-name $AgentName
#     if ($LASTEXITCODE -ne 0) {
#         Write-Warning "a365 cleanup instance returned exit code $LASTEXITCODE — continuing."
#     }

#     Write-Host "Cleaning up A365 agent blueprint..."
#     a365 cleanup blueprint --verbose --yes --agent-name $AgentName
#     if ($LASTEXITCODE -ne 0) {
#         Write-Warning "a365 cleanup blueprint returned exit code $LASTEXITCODE — continuing."
#     }
# } finally {
#     Pop-Location
# }
