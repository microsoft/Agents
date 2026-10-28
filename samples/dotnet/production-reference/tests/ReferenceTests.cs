// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ProductionReference.Tests;

public sealed class ReferenceTests
{
    private const string ClientId = "11111111-1111-1111-1111-111111111111";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes(new string('k', 64)));

    private static Dictionary<string, string?> Configuration() => new()
    {
        ["Connections:ServiceConnection:Settings:ClientId"] = ClientId,
        ["Connections:ServiceConnection:Settings:TenantId"] = "22222222-2222-2222-2222-222222222222",
        ["Storage:ServiceUrl"] = "https://example.blob.core.windows.net",
        ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=33333333-3333-3333-3333-333333333333",
    };

    [Theory]
    [InlineData("Connections:ServiceConnection:Settings:ClientId")]
    [InlineData("Connections:ServiceConnection:Settings:TenantId")]
    [InlineData("Storage:ServiceUrl")]
    [InlineData("APPLICATIONINSIGHTS_CONNECTION_STRING")]
    public void MissingProductionConfigurationFails(string key)
    {
        var values = Configuration();
        values.Remove(key);
        Assert.Throws<ArgumentException>(() => AppConfig.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Theory]
    [InlineData("Connections:ServiceConnection:Settings:ClientSecret", "secret")]
    [InlineData("Storage:ConnectionString", "secret")]
    [InlineData("Storage:ServiceUrl", "http://example.blob.core.windows.net")]
    [InlineData("Storage:ServiceUrl", "https://example.blob.core.windows.net?sig=secret")]
    [InlineData("APP_ENV", "staging")]
    [InlineData("Connections:ServiceConnection:Settings:AuthType", "ClientSecret")]
    [InlineData("Connections:ServiceConnection:Settings:ValidateIssuer", "false")]
    [InlineData("Connections:ServiceConnection:Settings:Scopes:0", "https://evil.test/.default")]
    [InlineData("Connections:ServiceConnection:Settings:Scopes:1", "https://evil.test/.default")]
    [InlineData("Connections:Scopes:0", "https://api.botframework.com/.default")]
    [InlineData("ConnectionsMap:0:Audience", "wrong")]
    [InlineData("ConnectionsMap:0:Connection", "wrong")]
    [InlineData("TokenValidation:Audiences:0", "wrong")]
    [InlineData("TokenValidation:Audiences:1", "wrong")]
    [InlineData("TokenValidation:TenantId", "wrong")]
    [InlineData("OutboundHostValidator:Enabled", "false")]
    [InlineData("OutboundHostValidator:Hosts:0", "evil.test")]
    public void UnsafeProductionConfigurationFails(string key, string value)
    {
        var values = Configuration();
        values[key] = value;
        Assert.Throws<ArgumentException>(() => AppConfig.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Fact]
    public void JsonSettingsMatchProductionPolicyAndDevelopmentOverrides()
    {
        var production = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddInMemoryCollection(Configuration()).Build();
        Assert.True(AppConfig.Load(production).Production);
        var development = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"))
            .Build();
        Assert.False(AppConfig.Load(development).Production);
    }

    [Fact]
    public void CapturePreservesCompletedState()
    {
        var (state, _) = IssueCapture.Advance(new(), "Checkout is unavailable");
        Assert.Equal("impact", state.Stage);
        Assert.Equal(state, IssueCapture.Advance(state, "urgent").State);
        var completed = IssueCapture.Advance(state, " HIGH ").State;
        Assert.Equal("high", completed.Impact);
        Assert.Equal(completed, IssueCapture.Advance(completed, "another issue").State);
        Assert.Throws<InvalidOperationException>(() => IssueCapture.Advance(new(Version: 2), "issue"));
    }

    [Theory]
    [InlineData("https://webchat.botframework.com/", true)]
    [InlineData("https://webchat.botframework.com.evil.test/", false)]
    [InlineData("https://user:secret@webchat.botframework.com/", false)]
    [InlineData("https://webchat.botframework.com:444/", false)]
    [InlineData("http://webchat.botframework.com/", false)]
    [InlineData("http://localhost:3978/", false)]
    public void ProductionServiceUrlPolicy(string url, bool expected)
    {
        Assert.Equal(expected, AppHost.AllowedServiceUrl(url, true));
    }

    private static async Task<WebApplication> Start(FakeStorage? storage = null, FakeAdapter? adapter = null)
    {
        var app = AppHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(Configuration());
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IStorage>(storage ?? new FakeStorage());
            // AppHost uses TryAdd for adapter registration. Fake only activity
            // processing, keeping real ASP.NET JWT signature/claims validation.
            builder.Services.AddSingleton<IAgentHttpAdapter>(adapter ?? new FakeAdapter());
            builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = AppHost.BotIssuer };
                metadata.SigningKeys.Add(Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                options.TokenValidationParameters.IssuerSigningKey = Key;
                options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            });
        });
        await app.StartAsync();
        return app;
    }

    private static string Token(string? audience = null, string? issuer = null, bool expired = false, string? serviceUrl = null)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer ?? AppHost.BotIssuer,
            audience: audience ?? ClientId,
            claims: serviceUrl == null ? [] : [new Claim("serviceurl", serviceUrl)],
            notBefore: expired ? now.AddHours(-2) : now.AddMinutes(-1),
            expires: expired ? now.AddHours(-1) : now.AddMinutes(10),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string? token = null, string url = "https://webchat.botframework.com/", string channel = "webchat")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = JsonContent.Create(new { serviceUrl = url, channelId = channel, type = "message" }),
        };
        if (token != null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task JwtSignatureAudienceIssuerLifetimeAndChannelAreEnforced()
    {
        var adapter = new FakeAdapter();
        await using var app = await Start(adapter: adapter);
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        foreach (var token in new[] { null, "malformed", Token(audience: "wrong"), Token(issuer: "https://evil.test"), Token(expired: true) })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, token)).StatusCode);
        }
        Assert.Equal(0, adapter.Calls);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Token(), url: "https://evil.test/")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Token(), channel: "msteams")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, Token(serviceUrl: "https://webchat.botframework.com/other"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await Send(client, Token())).StatusCode);
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public async Task RealCloudAdapterSanitizesRethrownAgentErrors()
    {
        await using var app = AppHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(Configuration());
            builder.Services.AddSingleton<IStorage>(new FakeStorage { Fail = true });
        });
        var adapter = app.Services.GetRequiredService<CloudAdapter>();
        var agent = app.Services.GetRequiredService<IAgent>();
        using var context = new TurnContext(adapter, new Activity
        {
            Type = ActivityTypes.Message,
            ChannelId = "webchat",
            ServiceUrl = "https://webchat.botframework.com/",
            Conversation = new ConversationAccount { Id = "private-conversation" },
            From = new ChannelAccount { Id = "user" },
            Recipient = new ChannelAccount { Id = "agent" },
            Text = "Checkout is unavailable",
        });
        var outgoing = new List<IActivity>();
        // Intercept only outbound transport. Run the real CloudAdapter middleware
        // pipeline, including AgentApplication's rethrow and adapter error handler.
        context.OnSendActivities((turn, activities, next) =>
        {
            outgoing.AddRange(activities);
            return Task.FromResult(activities.Select(_ => new ResourceResponse { Id = "reply" }).ToArray());
        });
        var pipeline = typeof(ChannelAdapter).GetMethod("RunPipelineAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        AgentCallbackHandler callback = agent.OnTurnAsync;
        await (Task)pipeline.Invoke(adapter, [context, callback, CancellationToken.None])!;
        var reply = Assert.Single(outgoing);
        Assert.Equal(ActivityTypes.Message, reply.Type);
        Assert.Equal("Sorry, the request could not be processed. Please try again later.", reply.Text);
        Assert.DoesNotContain("secret", reply.Text);
        Assert.DoesNotContain("private-conversation", reply.Text);
    }

    [Fact]
    public async Task PayloadLimitAndSafeErrors()
    {
        var adapter = new FakeAdapter { Fail = true };
        await using var app = await Start(adapter: adapter);
        var client = app.GetTestClient();
        var response = await Send(client, Token());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = new StringContent(new string('x', AppHost.PayloadLimit + 1)),
        };
        request.Headers.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task StorageFailureAffectsReadinessAndShutdownCompletes()
    {
        await using var app = await Start(new FakeStorage { Fail = true });
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        await app.StopAsync();
        Assert.True(app.Lifetime.ApplicationStopped.IsCancellationRequested);
    }

    [Fact]
    public async Task ReadinessChecksWritableStorage()
    {
        var storage = new FakeStorage();
        await using var app = await Start(storage);
        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().GetAsync("/health/ready")).StatusCode);
        Assert.Equal(1, storage.Writes);
        Assert.Equal(1, storage.Deletes);
    }

    [BlobIntegrationFact]
    public async Task FirstWriteAndStaleWriteCannotOverwriteState()
    {
        var container = new BlobContainerClient(
            Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING"),
            $"production-reference-test-{Guid.NewGuid():N}");
        var first = new VersionedStorage(container);
        var second = new VersionedStorage(container);
        const string key = "conversation.test";
        try
        {
            await first.WriteAsync(new Dictionary<string, object>
            {
                [key] = new Dictionary<string, object> { ["stage"] = "impact" },
            });
            await Assert.ThrowsAsync<RequestFailedException>(() => second.WriteAsync(
                new Dictionary<string, object> { [key] = new Dictionary<string, object>() }));
            var loaded = await second.ReadAsync([key]);
            var stale = await first.ReadAsync([key]);
            ((IDictionary<string, object>)loaded[key])["stage"] = "complete";
            await second.WriteAsync(loaded);
            await Assert.ThrowsAsync<RequestFailedException>(() => first.WriteAsync(stale));
            var recovered = await first.ReadAsync([key]);
            Assert.Equal("complete", ((IDictionary<string, object>)recovered[key])["stage"].ToString());
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public Task AgentRecoversConversationAcrossInstances()
        => VerifyConversationRecovery(new MemoryStorage());

    [BlobIntegrationFact]
    public async Task AgentRecoversConversationFromBlobAcrossInstances()
    {
        var container = new BlobContainerClient(
            Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING"),
            $"production-reference-recovery-{Guid.NewGuid():N}");
        try
        {
            await VerifyConversationRecovery(new VersionedStorage(container));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    private static async Task VerifyConversationRecovery(IStorage storage)
    {
        var replies = new ReplyAdapter();
        foreach (var text in new[] { "Checkout is unavailable", "high", "another issue" })
        {
            using var telemetry = new Telemetry();
            var agent = new SupportAgent(new AgentApplicationOptions(storage) { StartTypingTimer = false }, telemetry);
            using var context = new TurnContext(replies, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "webchat",
                ServiceUrl = "https://webchat.botframework.com/",
                Conversation = new ConversationAccount { Id = "recovery-test" },
                From = new ChannelAccount { Id = "user" },
                Recipient = new ChannelAccount { Id = "agent" },
                Text = text,
            });
            await agent.OnTurnAsync(context, CancellationToken.None);
        }
        Assert.Contains("impact", replies.Messages[0]);
        Assert.Contains("saved", replies.Messages[1]);
        Assert.Contains("completed", replies.Messages[2]);
    }
}

internal sealed class ReplyAdapter : ChannelAdapter
{
    public List<string> Messages { get; } = [];

    public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        Messages.AddRange(activities.Select(activity => activity.Text ?? ""));
        return Task.FromResult(activities.Select(_ => new ResourceResponse { Id = Guid.NewGuid().ToString() }).ToArray());
    }
}

internal sealed class FakeStorage : IStorage
{
    public bool Fail { get; init; }
    public int Writes { get; private set; }
    public int Deletes { get; private set; }
    public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
        => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());
    public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
        => Task.FromResult<IDictionary<string, TStoreItem>>(new Dictionary<string, TStoreItem>());
    public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
        => WriteAsync(changes.ToDictionary(item => item.Key, item => (object)item.Value), cancellationToken);
    public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
    {
        Writes++;
        return Fail ? Task.FromException(new InvalidOperationException("secret")) : Task.CompletedTask;
    }
    public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        Deletes++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAdapter : IAgentHttpAdapter
{
    public int Calls { get; private set; }
    public bool Fail { get; init; }

    public Task ProcessAsync(Microsoft.AspNetCore.Http.HttpRequest request, Microsoft.AspNetCore.Http.HttpResponse response, IAgent agent, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Fail)
        {
            throw new InvalidOperationException("token secret");
        }
        response.StatusCode = 202;
        return Task.CompletedTask;
    }
}

internal sealed class BlobIntegrationFactAttribute : FactAttribute
{
    public BlobIntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING")))
        {
            Skip = "Set AZURITE_CONNECTION_STRING to run Blob integration checks.";
        }
    }
}
