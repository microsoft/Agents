// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace ProductionReference;

public static class AppHost
{
    public const int PayloadLimit = 256 * 1024;
    public const string BotIssuer = "https://api.botframework.com";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var config = AppConfig.Load(builder.Configuration);
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<Telemetry>();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = PayloadLimit);
        builder.Logging.ClearProviders();
        // SDK/framework diagnostics can contain URLs, identifiers and exception
        // messages. Only the bounded sample metrics/spans are exported here.
        if (config.Production)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                    builder.Configuration["OTEL_SERVICE_NAME"] ?? "agents-sdk-production-reference-dotnet"))
                .WithTracing(tracing => tracing.AddSource(Telemetry.SourceName)
                    .AddAzureMonitorTraceExporter(options => options.ConnectionString = config.TelemetryConnectionString))
                .WithMetrics(metrics => metrics.AddMeter(Telemetry.SourceName)
                    .AddAzureMonitorMetricExporter(options => options.ConnectionString = config.TelemetryConnectionString));
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Connections:ServiceConnection:Assembly"] = "Microsoft.Agents.Authentication.Msal",
            ["Connections:ServiceConnection:Type"] = "Microsoft.Agents.Authentication.Msal.MsalAuth",
            ["Connections:ServiceConnection:Settings:ClientId"] = config.ClientId,
            ["Connections:ServiceConnection:Settings:TenantId"] = config.TenantId,
            ["Connections:ServiceConnection:Settings:AuthType"] = config.Production ? "UserManagedIdentity" : "ClientSecret",
            ["Connections:ServiceConnection:Settings:ClientSecret"] = config.ClientSecret,
            ["Connections:ServiceConnection:Settings:Scopes:0"] = "https://api.botframework.com/.default",
            ["ConnectionsMap:0:ServiceUrl"] = "*",
            ["ConnectionsMap:0:Connection"] = "ServiceConnection",
            ["ConnectionsMap:0:Audience"] = config.ClientId,
            ["TokenValidation:Audiences:0"] = config.ClientId,
            ["TokenValidation:TenantId"] = config.TenantId,
            ["AgentApplication:StartTypingTimer"] = "false",
            ["AgentApplication:RemoveRecipientMention"] = "false",
        });
        builder.Services.AddSingleton<IOutboundHostValidator>(new OutboundHostValidator(new()
        {
            Enabled = config.Production,
            IncludeDefaultMicrosoftHosts = false,
            Hosts = ["webchat.botframework.com"],
        }));
        builder.Services.TryAddSingleton<IStorage>(_ => new VersionedStorage(config.Production
            ? new BlobContainerClient(new Uri($"{config.BlobServiceUrl.TrimEnd('/')}/{config.BlobContainer}"),
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(config.ClientId)))
            : new BlobContainerClient(config.BlobConnectionString, config.BlobContainer)));
        builder.Services.AddSingleton<SafeAdapterErrorHandler>();
        builder.Services.AddSingleton<CloudAdapter>(services =>
        {
            var adapter = ActivatorUtilities.CreateInstance<CloudAdapter>(services);
            adapter.OnTurnError = services.GetRequiredService<SafeAdapterErrorHandler>().HandleTurnErrorAsync;
            return adapter;
        });
        builder.AddAgentDefaults().AddAgent<SupportAgent>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.MetadataAddress = "https://login.botframework.com/v1/.well-known/openidconfiguration";
            options.RequireHttpsMetadata = true;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = BotIssuer,
                ValidateAudience = true,
                ValidAudience = config.ClientId,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        var telemetry = app.Services.GetRequiredService<Telemetry>();
        app.Use(async (context, next) =>
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            catch (Exception error) when (!context.Response.HasStarted)
            {
                telemetry.Failure(error is BadHttpRequestException ? "payload" : "http");
                context.Response.Clear();
                context.Response.StatusCode = error is BadHttpRequestException bad ? bad.StatusCode : error is JsonException ? 400 : 500;
                await context.Response.WriteAsJsonAsync(new { error = "Request could not be processed." });
            }
            finally
            {
                telemetry.Request(context.Response.StatusCode, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
            }
        });
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health/live", () => Results.Json(new { status = "ok" })).AllowAnonymous();
        app.MapGet("/health/ready", async (IStorage storage) =>
        {
            var key = $"health.readiness.{Guid.NewGuid()}";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await storage.WriteAsync(new Dictionary<string, object> { [key] = new { checkedAt = DateTimeOffset.UtcNow } }, timeout.Token);
                await storage.DeleteAsync([key], timeout.Token);
                return Results.Json(new { status = "ready" });
            }
            catch
            {
                telemetry.Failure("readiness");
                return Results.Json(new { status = "not ready" }, statusCode: 503);
            }
        }).AllowAnonymous();
        var messages = app.MapPost("/api/messages", async (HttpContext context, IAgentHttpAdapter adapter, IAgent agent) =>
        {
            // A bounded read also protects chunked requests and TestServer hosts.
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (body.Length + count > PayloadLimit)
                {
                    throw new BadHttpRequestException("Payload too large.", 413);
                }
                await body.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted);
            }
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: context.RequestAborted);
            var activity = document.RootElement;
            if (activity.ValueKind != JsonValueKind.Object)
            {
                throw new BadHttpRequestException("Invalid activity.", 400);
            }
            var serviceUrl = activity.TryGetProperty("serviceUrl", out var service) && service.ValueKind == JsonValueKind.String
                ? service.GetString() : null;
            if (!AllowedServiceUrl(serviceUrl, config.Production)
                || (config.Production && (!activity.TryGetProperty("channelId", out var channel) || channel.GetString() != "webchat")))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Request could not be processed." });
                return;
            }
            var serviceClaim = context.User.FindFirst("serviceurl")?.Value;
            if (serviceClaim != null && serviceClaim != serviceUrl)
            {
                context.Response.StatusCode = 401;
                return;
            }
            body.Position = 0;
            context.Request.Body = body;
            await adapter.ProcessAsync(context.Request, context.Response, agent, context.RequestAborted);
        });
        if (config.Production)
        {
            messages.RequireAuthorization();
        }
        else
        {
            messages.AllowAnonymous();
        }
        app.Lifetime.ApplicationStopped.Register(() =>
        {
            // Kestrel has drained requests before ApplicationStopped. Flush before
            // DI disposes the providers/exporters.
            app.Services.GetService<TracerProvider>()?.ForceFlush(5000);
            app.Services.GetService<MeterProvider>()?.ForceFlush(5000);
        });
        return app;
    }

    public static bool AllowedServiceUrl(string? value, bool production)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            return false;
        }
        if (uri.Host.Equals("webchat.botframework.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Scheme == "https" && uri.Port == 443;
        }
        return !production && uri.Scheme is "http" or "https"
            && uri.Host is "localhost" or "127.0.0.1" or "[::1]";
    }
}
