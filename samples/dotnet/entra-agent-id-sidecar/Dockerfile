FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EntraAgentIdSidecar.csproj .
RUN dotnet restore EntraAgentIdSidecar.csproj

COPY . .
RUN dotnet publish EntraAgentIdSidecar.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:3978
EXPOSE 3978
USER app

ENTRYPOINT ["dotnet", "EntraAgentIdSidecar.dll"]
