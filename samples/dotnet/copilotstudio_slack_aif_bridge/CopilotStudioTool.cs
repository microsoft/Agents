using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Slack_MCS_Bridge
{
    public class CopilotStudioTool
    {
        private readonly ITurnContext _context;
        private readonly ITurnState _state;
        private readonly IConfiguration _configuration;
        private readonly string _authHandlerName;
        private readonly IServiceProvider _sp;
        private readonly ILogger _logger;
        private readonly UserAuthorization _authSystem;
        private readonly string MCSConversationPropertyName = "MCSConversationId";

        public CopilotStudioTool(IServiceProvider serviceProvider,
            ITurnContext context, ITurnState state,
            IConfiguration configuration, ILogger logger,
            UserAuthorization authSystem,
            string authHandlerName)
        {
            _context = context;
            _state = state;
            _configuration = configuration;
            _authHandlerName = authHandlerName;
            _sp = serviceProvider;
            _authSystem = authSystem;
            _logger = logger;
        }

        /// <summary>
        /// Sends a request to Copilot Studio and preserves the returned activities, including attachments.
        /// </summary>
        [Description("Processes a request using Copilot Studio and streams the response. ")]
        public async IAsyncEnumerable<IActivity> ProcessCopilotStudioRequest(
            string request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = new ConnectionSettings(_configuration.GetSection("CopilotStudioAgent"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];
            var mcsToken = await _authSystem.ExchangeTurnTokenAsync(
                _context, _authHandlerName, exchangeScopes: scopes, cancellationToken: cancellationToken).ConfigureAwait(false);
            var cpsClient = GetClient(mcsToken);
            var mcsConversationId = _state.Conversation.GetValue<string>(MCSConversationPropertyName);

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                await foreach (IActivity activity in cpsClient.StartConversationAsync(
                    emitStartConversationEvent: true, cancellationToken: cancellationToken))
                {
                    if (!string.IsNullOrEmpty(activity.Conversation?.Id))
                    {
                        _state.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                        mcsConversationId = activity.Conversation.Id;
                    }
                }
            }

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                throw new InvalidOperationException("Copilot Studio did not return a conversation ID.");
            }
            await foreach (IActivity activity in cpsClient.AskQuestionAsync(request, mcsConversationId, cancellationToken))
            {
                yield return activity;
            }
        }


        private CopilotClient GetClient(string accessToken)
        {
            var settings = new ConnectionSettings(_configuration.GetSection("CopilotStudioAgent"));
            return new CopilotClient(
                settings,
                _sp.GetRequiredService<IHttpClientFactory>(),
                tokenProviderFunction: _ => Task.FromResult(accessToken),
                _logger,
                _authHandlerName);
        }
    }
}
