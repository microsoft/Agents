using './main.bicep'

param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'e2e-agentic')
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus')
param deployerPrincipalId = readEnvironmentVariable('AZURE_PRINCIPAL_ID', '')
