// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Claims;
using Discord;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
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
    public class DiscordAdapter(ILogger<DiscordAdapter> logger) : ChannelAdapter(logger)
    {
        /// <summary>Discord's channel id used on the Activity.</summary>
        public const string ChannelId = "discord";

        private readonly ILogger<DiscordAdapter> _logger = logger;

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
            var context = new TurnContext(this, activity, claimsIdentity);
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            return null;
        }

        /// <summary>
        /// OUTBOUND: render each reply message as a Discord embed and post it to the channel.
        /// </summary>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(
            ITurnContext turnContext,
            IActivity[] activities,
            CancellationToken cancellationToken)
        {
            var responses = new List<ResourceResponse>();

            foreach (var activity in activities)
            {
                // Only render actual messages; skip typing/informative activities.
                if (activity.Type != ActivityTypes.Message || string.IsNullOrWhiteSpace(activity.Text))
                {
                    responses.Add(new ResourceResponse());
                    continue;
                }

                var conversationId = activity.Conversation?.Id ?? turnContext.Activity.Conversation?.Id;
                if (conversationId == null || !_channels.TryGetValue(conversationId, out var channel))
                {
                    _logger.LogWarning("No Discord channel registered for conversation {ConversationId}", conversationId);
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
    }
}
