using './main.bicep'

param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'e2e-local')
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus')
param deployerPrincipalId = readEnvironmentVariable('AZURE_PRINCIPAL_ID', '')

// Override to use a devtunnel URL for JWT auth scenarios:
// param botEndpoint = 'https://<tunnel-id>.devtunnels.ms/api/messages'
