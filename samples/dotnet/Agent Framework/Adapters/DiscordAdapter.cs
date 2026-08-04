// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Discord;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using IActivity = Microsoft.Agents.Core.Models.IActivity;

namespace AgentFrameworkWeather.Adapters
{
    /// <summary>
    /// A custom <see cref="ChannelAdapter"/> for Discord.
    ///
    /// Discord has no built-in Azure Bot Service channel, so this adapter is the translation
    /// layer between the Discord gateway and the Agents SDK, exactly like the SDK's A2AAdapter:
    ///   * INBOUND  - <see cref="DiscordGatewayService"/> turns a Discord message into an Activity
    ///                and calls <see cref="ProcessActivityAsync"/>, which drives the shared
    ///                <c>WeatherAgent</c> (AgentApplication) through the SDK turn pipeline.
    ///   * OUTBOUND - <see cref="SendActivitiesAsync"/> renders the agent's reply Activity as a
    ///                Discord embed and posts it back to the originating channel.
    ///
    /// The same WeatherAgent is reused unchanged; only this adapter differs from Slack/Teams.
    /// </summary>
    public class DiscordAdapter(ILogger<DiscordAdapter> logger, IChannelServiceClientFactory channelServiceClientFactory) : ChannelAdapter(logger)
    {
        /// <summary>Discord's channel id used on the Activity.</summary>
        public const string ChannelId = "discord";

        private readonly ILogger<DiscordAdapter> _logger = logger;
        private readonly IChannelServiceClientFactory _channelServiceClientFactory = channelServiceClientFactory;

        // Maps an Activity conversation id (the Discord channel id) to the live Discord channel,
        // so SendActivitiesAsync knows where to post the reply.
        private readonly ConcurrentDictionary<string, IMessageChannel> _channels = new();

        /// <summary>
        /// Registers the Discord channel for a conversation so replies can be routed back to it.
        /// Called by the gateway service when an inbound message arrives.
        /// </summary>
        public void RegisterChannel(string conversationId, IMessageChannel channel)
            => _channels[conversationId] = channel;

        /// <summary>
        /// INBOUND: run one agent turn for the given activity. Identical shape to A2AAdapter -
        /// build a TurnContext and run the SDK pipeline (which invokes the agent's OnTurnAsync).
        /// </summary>
        public override async Task<InvokeResponse> ProcessActivityAsync(
            ClaimsIdentity claimsIdentity,
            IActivity activity,
            AgentCallbackHandler callback,
            CancellationToken cancellationToken)
        {
            await RunPipelineWithUserTokenAsync(claimsIdentity, activity, callback, cancellationToken).ConfigureAwait(false);
            return null!;
        }

        /// <summary>
        /// PROACTIVE: after a sign-in completes, the SDK re-runs the original ("banked") activity via
        /// this method. It must set up the same IUserTokenClient as ProcessActivityAsync, otherwise the
        /// resumed turn (which reads the user token) fails with "IUserTokenClient is not available".
        /// </summary>
        public override Task ProcessProactiveAsync(
            ClaimsIdentity claimsIdentity,
            IActivity continuationActivity,
            string audience,
            AgentCallbackHandler callback,
            CancellationToken cancellationToken)
            => RunPipelineWithUserTokenAsync(claimsIdentity, continuationActivity, callback, cancellationToken);

        /// <summary>
        /// Build a TurnContext, attach an IUserTokenClient (Discord isn't an Azure Bot channel, so the
        /// base ChannelAdapter doesn't create one), and run the SDK turn pipeline. Used by both the
        /// inbound and proactive (post-sign-in re-run) paths so the OAuth/OBO flow works over Discord.
        /// </summary>
        private async Task RunPipelineWithUserTokenAsync(
            ClaimsIdentity claimsIdentity,
            IActivity activity,
            AgentCallbackHandler callback,
            CancellationToken cancellationToken)
        {
            var context = new TurnContext(this, activity, claimsIdentity);

            using var userTokenClient = await _channelServiceClientFactory
                .CreateUserTokenClientAsync(claimsIdentity, useAnonymous: null, cancellationToken).ConfigureAwait(false);
            context.Services.Set(userTokenClient);
            context.Services.Set(_channelServiceClientFactory);

            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// OUTBOUND: render each reply message as a Discord embed and post it to the channel.
        /// Sign-in (OAuthCard) activities are rendered as a clickable sign-in link.
        /// </summary>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(
            ITurnContext turnContext,
            IActivity[] activities,
            CancellationToken cancellationToken)
        {
            var responses = new List<ResourceResponse>();

            foreach (var activity in activities)
            {
                // OAuth sign-in: Discord isn't a known Bot Service channel, so the OAuthCard carries
                // no usable link. Fetch the real sign-in URL and post it as a Discord message.
                if (TryGetOAuthConnectionName(activity, out var connectionName))
                {
                    responses.Add(await SendSignInAsync(turnContext, connectionName, cancellationToken).ConfigureAwait(false));
                    continue;
                }

                // Only render actual messages; skip typing/informative activities.
                if (activity.Type != ActivityTypes.Message || string.IsNullOrWhiteSpace(activity.Text))
                {
                    responses.Add(new ResourceResponse());
                    continue;
                }

                if (!TryGetChannel(activity, turnContext, out var channel))
                {
                    responses.Add(new ResourceResponse());
                    continue;
                }

                // Discord embed description limit is 4096; trim defensively.
                var body = activity.Text;
                if (body.Length > 4000)
                {
                    body = body[..4000] + "…";
                }

                var embed = new EmbedBuilder()
                    .WithColor(new Color(0x2E, 0x9B, 0xF5))
                    .WithAuthor("🐾 Purrfect Assistant")
                    .WithDescription(body)
                    .WithFooter("Agent Framework + WorkIQ · Discord ChannelAdapter")
                    .WithCurrentTimestamp()
                    .Build();

                var sent = await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                responses.Add(new ResourceResponse { Id = sent.Id.ToString() });
            }

            return [.. responses];
        }

        /// <summary>Resolve the live Discord channel for the conversation the activity belongs to.</summary>
        private bool TryGetChannel(IActivity activity, ITurnContext turnContext, out IMessageChannel channel)
        {
            var conversationId = activity.Conversation?.Id ?? turnContext.Activity.Conversation?.Id;
            if (conversationId == null || !_channels.TryGetValue(conversationId, out channel!))
            {
                _logger.LogWarning("No Discord channel registered for conversation {ConversationId}", conversationId);
                channel = null!;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Post the OAuth sign-in link to Discord. The IUserTokenClient (set on the turn) resolves the
        /// real sign-in URL from the Bot Framework Token Service; the user clicks it, signs in, and
        /// pastes the returned code back into the chat to complete the flow.
        /// </summary>
        private async Task<ResourceResponse> SendSignInAsync(ITurnContext turnContext, string connectionName, CancellationToken cancellationToken)
        {
            if (!TryGetChannel(turnContext.Activity, turnContext, out var channel))
            {
                return new ResourceResponse();
            }

            var userTokenClient = turnContext.Services.Get<IUserTokenClient>();
            var signInResource = await userTokenClient
                .GetSignInResourceAsync(connectionName, turnContext.Activity, null, cancellationToken).ConfigureAwait(false);
            var link = signInResource?.SignInLink;
            if (string.IsNullOrEmpty(link))
            {
                _logger.LogWarning("[Discord] No sign-in link available for connection {Connection}", connectionName);
                return new ResourceResponse();
            }

            var embed = new EmbedBuilder()
                .WithColor(new Color(0x2E, 0x9B, 0xF5))
                .WithAuthor("🐾 Purrfect Assistant")
                .WithTitle("Sign in required")
                .WithDescription($"Please [sign in here]({link}) to continue, then paste the code you receive back into this chat.")
                .WithFooter("Agent Framework + WorkIQ · Discord ChannelAdapter")
                .WithCurrentTimestamp()
                .Build();

            var sent = await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            return new ResourceResponse { Id = sent.Id.ToString() };
        }

        /// <summary>Detect an OAuthCard attachment and return its OAuth connection name.</summary>
        private static bool TryGetOAuthConnectionName(IActivity activity, out string connectionName)
        {
            connectionName = null!;
            if (activity.Attachments is null)
            {
                return false;
            }

            foreach (var attachment in activity.Attachments)
            {
                if (!string.Equals(attachment.ContentType, "application/vnd.microsoft.card.oauth", StringComparison.OrdinalIgnoreCase)
                    || attachment.Content is null)
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(ProtocolJsonSerializer.ToJson(attachment.Content));
                    if (doc.RootElement.TryGetProperty("connectionName", out var cn))
                    {
                        connectionName = cn.GetString()!;
                        if (!string.IsNullOrEmpty(connectionName))
                        {
                            return true;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not a parseable OAuthCard; ignore.
                }
            }

            return false;
        }
    }
}
