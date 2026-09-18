// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Agents.Authentication;
using BotAiMessages;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Teams.Samples.BotCards;
using Xunit;
using Microsoft.Agents.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "bot-cards")]
    public async Task BotCards_CardActions_ReturnsAdaptiveCardAsync()
    {
        await using AgentTestHost host = AgentTestHost.Create(builder =>
        {
            builder.Services.AddSingleton<IStorage, MemoryStorage>();
            builder.Services.AddHttpClient();
            builder.Services.AddTransient<IAgent>(services =>
                new BotCardsAgent(CreateOptions(services)));
        });
        host.Adapter.Conversation.ChannelId = Channels.Msteams;

        await host.CreateTestFlow()
            .Send("card actions")
            .AssertReply(activity =>
            {
                Assert.Single(activity.Attachments);
                Assert.Equal(ContentTypes.AdaptiveCard, activity.Attachments[0].ContentType);
            })
            .AssertNoMoreReplies()
            .StartTestAsync();
    }

    [Fact]
    [Trait("Sample", "bot-ai-messages")]
    public async Task BotAiMessages_UnknownText_ReturnsStableHelpAsync()
    {
        await using AgentTestHost host = AgentTestHost.Create(builder =>
        {
            builder.Services.AddSingleton<IStorage, MemoryStorage>();
            builder.Services.AddHttpClient();
            builder.Services.AddTransient<IAgent>(services =>
                new BotAiMessagesAgent(CreateOptions(services)));
        });
        host.Adapter.Conversation.ChannelId = Channels.Msteams;

        await host.CreateTestFlow()
            .Send("unknown")
            .AssertReplyContains("Welcome to the AI bot")
            .AssertNoMoreReplies()
            .StartTestAsync();
    }

    private static JObject Payload(object value) => JObject.Parse(
        System.Text.Json.JsonSerializer.Serialize(ProtocolJsonSerializer.ToJsonElements(value)));

    private static AgentTestHost CreateHost(Func<IServiceProvider, IAgent> agentFactory)
    {
        AgentTestHost host = AgentTestHost.Create(builder =>
        {
            builder.Services.AddSingleton<IStorage, MemoryStorage>();
            builder.Services.AddHttpClient();
            builder.Services.AddTransient(agentFactory);
        });
        host.Adapter.Conversation.ChannelId = Channels.Msteams;
        return host;
    }

    private static AgentApplicationOptions CreateOptions(IServiceProvider services)
    {
        IAccessTokenProvider tokenProvider = new TestTokenProvider();
        IConnections connections = new ConfigurationConnections(
            new Dictionary<string, IAccessTokenProvider> { ["test"] = tokenProvider },
            [],
            NullLogger<ConfigurationConnections>.Instance);

        return new AgentApplicationOptions(services.GetRequiredService<IStorage>())
        {
            Connections = connections,
            HttpClientFactory = services.GetRequiredService<IHttpClientFactory>()
        };
    }

    private sealed class TestTokenProvider : IAccessTokenProvider
    {
        public ImmutableConnectionSettings ConnectionSettings { get; } = new(new TestConnectionSettings());

        public Task<string> GetAccessTokenAsync(string resource, IList<string> scopes, bool forceRefresh)
            => Task.FromResult(string.Empty);

        public TokenCredential GetTokenCredential() => new TestCredential();
    }

    private sealed class TestConnectionSettings : ConnectionSettingsBase;

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(string.Empty, DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new AccessToken(string.Empty, DateTimeOffset.MaxValue));
    }
}
