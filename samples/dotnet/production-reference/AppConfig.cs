// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace ProductionReference;

public sealed record AppConfig(
    bool Production,
    string ClientId,
    string TenantId,
    string BlobServiceUrl,
    string BlobContainer,
    string BlobConnectionString,
    string ClientSecret,
    string TelemetryConnectionString)
{
    public static AppConfig Load(IConfiguration configuration)
    {
        string Read(string key) => configuration[key]?.Trim() ?? "";
        string Required(string key) => Read(key) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"{key} is required.");

        var environment = Read("APP_ENV");
        if (environment.Length == 0)
        {
            environment = "production";
        }
        if (environment is not ("production" or "development"))
        {
            throw new ArgumentException("APP_ENV must be production or development.");
        }
        var production = environment == "production";
        var clientId = Read("Connections:ServiceConnection:Settings:ClientId");
        var tenantId = Read("Connections:ServiceConnection:Settings:TenantId");
        var serviceUrl = Read("Storage:ServiceUrl");
        var connectionString = Read("Storage:ConnectionString");
        var clientSecret = Read("Connections:ServiceConnection:Settings:ClientSecret");
        if (production)
        {
            var expectedPolicy = new Dictionary<string, string>
            {
                ["Connections:ServiceConnection:Settings:AuthType"] = "UserManagedIdentity",
                ["Connections:ServiceConnection:Settings:ValidateIssuer"] = "true",
                ["Connections:ServiceConnection:Settings:Scopes:0"] = "https://api.botframework.com/.default",
                ["ConnectionsMap:0:Connection"] = "ServiceConnection",
                ["ConnectionsMap:0:ServiceUrl"] = "*",
                ["ConnectionsMap:0:Audience"] = clientId,
                ["OutboundHostValidator:Enabled"] = "true",
                ["OutboundHostValidator:IncludeDefaultMicrosoftHosts"] = "false",
                ["OutboundHostValidator:Hosts:0"] = "webchat.botframework.com",
            };
            foreach (var (key, expected) in expectedPolicy)
            {
                if (key == "ConnectionsMap:0:Audience" && Read(key).Length == 0)
                {
                    continue;
                }
                if (configuration[key] != null && !Read(key).Equals(expected,
                    expected is "true" or "false" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{key} must match the production Web Chat policy.");
                }
            }
            if (configuration.GetSection("OutboundHostValidator:Hosts").GetChildren().Count() > 1
                || configuration.GetSection("ConnectionsMap").GetChildren().Count() > 1)
            {
                throw new ArgumentException("Production permits only the bounded Web Chat policy.");
            }
            var scopes = configuration.GetSection("Connections:ServiceConnection:Settings:Scopes").GetChildren().ToArray();
            if (scopes.Length > 0 && (scopes.Length != 1 || scopes[0].Key != "0"
                || scopes[0].Value != "https://api.botframework.com/.default"))
            {
                throw new ArgumentException("Production permits only the Bot Framework outbound scope.");
            }
            if (configuration.GetSection("Connections:Scopes").GetChildren().Any())
            {
                throw new ArgumentException("Outbound scopes must be configured on the service connection.");
            }
            var audiences = configuration.GetSection("TokenValidation:Audiences").GetChildren().ToArray();
            if (audiences.Length > 0 && (audiences.Length != 1 || audiences[0].Value != clientId))
            {
                throw new ArgumentException("TokenValidation:Audiences must contain only the service connection client ID.");
            }
            if (Read("TokenValidation:TenantId").Length > 0 && Read("TokenValidation:TenantId") != tenantId)
            {
                throw new ArgumentException("TokenValidation:TenantId must match the service connection tenant ID.");
            }
            if (!Guid.TryParse(Required("Connections:ServiceConnection:Settings:ClientId"), out _)
                || !Guid.TryParse(Required("Connections:ServiceConnection:Settings:TenantId"), out _))
            {
                throw new ArgumentException("Agent identity values must be GUIDs.");
            }
            if (!Uri.TryCreate(Required("Storage:ServiceUrl"), UriKind.Absolute, out var uri)
                || uri.Scheme != "https"
                || !uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)
                || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
                || uri.AbsolutePath != "/")
            {
                throw new ArgumentException("Storage:ServiceUrl must be an Azure public Blob service HTTPS URL.");
            }
            Required("APPLICATIONINSIGHTS_CONNECTION_STRING");
            if (connectionString.Length != 0 || clientSecret.Length != 0)
            {
                throw new ArgumentException("Production requires managed identity without connection strings or client secrets.");
            }
        }
        else if (connectionString.Length == 0)
        {
            throw new ArgumentException("Storage:ConnectionString is required for local Azurite state.");
        }
        var container = Read("Storage:Container");
        return new AppConfig(
            production, clientId, tenantId, serviceUrl,
            container.Length == 0 ? "agents-production-reference-state" : container,
            connectionString, clientSecret, Read("APPLICATIONINSIGHTS_CONNECTION_STRING"));
    }
}
