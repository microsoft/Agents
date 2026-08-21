$ErrorActionPreference = "Stop"

# This unsecured dashboard is for local development only.
$containerName = "aspire-dashboard"
$image = "mcr.microsoft.com/dotnet/aspire-dashboard:latest"

$existingContainer = docker container inspect $containerName 2>$null
if ($LASTEXITCODE -eq 0) {
    throw "A Docker container named '$containerName' already exists. Reuse or stop it before starting another dashboard."
}

docker run --rm --detach `
    --name $containerName `
    --publish "127.0.0.1:18888:18888" `
    --publish "127.0.0.1:4317:18889" `
    --publish "127.0.0.1:4318:18890" `
    --env "DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true" `
    $image

if ($LASTEXITCODE -ne 0) {
    throw "The Aspire Dashboard container failed to start."
}

Write-Host "Aspire Dashboard: http://localhost:18888"
Write-Host "OTLP/gRPC:       http://localhost:4317"
Write-Host "OTLP/HTTP:       http://localhost:4318"
Write-Warning "Anonymous access is enabled for local development only."
