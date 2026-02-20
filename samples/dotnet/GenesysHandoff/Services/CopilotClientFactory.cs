using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Factory for creating and configuring CopilotClient instances.
    /// </summary>
    public class CopilotClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CopilotClientFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Creates and configures a new instance of the CopilotClient for the specified agent application and turn context.
        /// </summary>
        /// <param name="app">The agent application used to provide user authorization for the CopilotClient.</param>
        /// <param name="turnContext">The current turn context containing information about the ongoing conversation and user state.</param>
        /// <returns>A configured CopilotClient instance that can be used to interact with Copilot Studio services on behalf of the user.</returns>
        public CopilotClient CreateClient(AgentApplication app, ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(turnContext);

            var settings = new ConnectionSettings(_configuration.GetSection("CopilotStudioAgent"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            return new CopilotClient(
                settings,
                _httpClientFactory,
                tokenProviderFunction: async (s) =>
                {
                    return await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
                },
                NullLogger.Instance,
                "mcs");
        }
    }
}
