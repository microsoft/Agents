using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProactiveMessaging
{
    public class ProactiveMessagingOptions
    {
        public string BotId { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string UserAadObjectId { get; set; } = string.Empty;
        public string Scope { get; set; } = "https://api.botframework.com/.default";
        public string ChannelId { get; set; } = "msteams";
        public string ServiceUrl { get; set; } = "https://smba.trafficmanager.net/teams/";
    }

    public class ProactiveMessenger : AgentApplication
    {
        private readonly ProactiveMessagingOptions _options;
        private readonly IChannelAdapter _adapter;

        public ProactiveMessenger(AgentApplicationOptions appOptions, IChannelAdapter adapter, IOptions<ProactiveMessagingOptions> options) : base(appOptions)
        {
            _options = options.Value;
            _adapter = adapter;
            // No inbound routes registered; this agent exists to enable proactive operations.
        }


        // New helper to create a conversation and return its id (optionally sending an initial message)
        public async Task<string> CreateConversationAsync(string? message, string? userAadObjectId, CancellationToken cancellationToken)
        {
            string? createdConversationId = null;
            var effectiveUserId = string.IsNullOrWhiteSpace(userAadObjectId) ? _options.UserAadObjectId : userAadObjectId;

            var parameters = new ConversationParameters(false,
                agent: new ChannelAccount(id: _options.AgentId),
                tenantId: _options.TenantId,
                members: new List<ChannelAccount>
                {
                    new ChannelAccount { Id = effectiveUserId }
                });

            await _adapter.CreateConversationAsync(
                _options.BotId,
                _options.ChannelId,
                _options.ServiceUrl,
                _options.Scope,
                parameters,
                async (ITurnContext turnContext, CancellationToken ct) =>
                {
                    createdConversationId = turnContext.Activity.Conversation.Id;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(message), ct);
                    }
                },
                default);

            if (string.IsNullOrEmpty(createdConversationId))
            {
                throw new System.InvalidOperationException("Failed to obtain conversation id after creation.");
            }

            return createdConversationId;
        }

        // Send a message to an existing conversation id
        public async Task SendMessageToConversationAsync(string conversationId, string message, CancellationToken cancellationToken)
        {
            var reference = BuildConversationReference(conversationId, null);
            await _adapter.ContinueConversationAsync(
                _options.BotId,
                reference,
                async (ITurnContext turnContext, CancellationToken ct) =>
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(message), ct);
                },
                default);
        }

        private ConversationReference BuildConversationReference(string conversationId, ConversationReference? existing)
        {
            if (existing != null)
            {
                return existing;
            }

            return new ConversationReference
            {
                Agent = new ChannelAccount { Id = _options.AgentId },
                Conversation = new ConversationAccount { Id = conversationId },
                ServiceUrl = _options.ServiceUrl
            };
        }

    }
}
