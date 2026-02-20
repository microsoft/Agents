using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Factory for creating and configuring CopilotClient instances.
    /// </summary>
    public class CopilotClientFactory
    {
        private const string McsHandlerName = "mcs";
        private const string ConfigurationSectionName = "CopilotStudioAgent";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CopilotClientFactory> _logger;

        public CopilotClientFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CopilotClientFactory> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Creates and configures a new instance of the CopilotClient for the specified agent application and turn context.
        /// </summary>
        /// <param name="app">The agent application used to provide user authorization for the CopilotClient.</param>
        /// <param name="turnContext">The current turn context containing information about the ongoing conversation and user state.</param>
        /// <returns>A configured CopilotClient instance that can be used to interact with Copilot Studio services on behalf of the user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when app or turnContext is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configuration section is missing or invalid.</exception>
        public CopilotClient CreateClient(AgentApplication app, ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(turnContext);

            try
            {
                // Validate that the configuration section exists
                var configSection = _configuration.GetSection(ConfigurationSectionName);
                if (!configSection.Exists())
                {
                    _logger.LogError("Configuration section '{ConfigurationSection}' is missing from the application configuration.", ConfigurationSectionName);
                    throw new InvalidOperationException($"Configuration section '{ConfigurationSectionName}' is missing. Please ensure it is properly configured in the application configuration.");
                }

                _logger.LogDebug("Creating CopilotClient with configuration from '{ConfigurationSection}'", ConfigurationSectionName);

                var settings = new ConnectionSettings(configSection);
                string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

                var client = new CopilotClient(
                    settings,
                    _httpClientFactory,
                    tokenProviderFunction: async (s) =>
                    {
                        try
                        {
                            var token = await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, McsHandlerName, exchangeScopes: scopes);
                            _logger.LogDebug("Successfully exchanged turn token for handler '{HandlerName}'", McsHandlerName);
                            return token;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to exchange turn token for handler '{HandlerName}'", McsHandlerName);
                            throw;
                        }
                    },
                    _logger,
                    McsHandlerName);

                _logger.LogInformation("Successfully created CopilotClient for handler '{HandlerName}'", McsHandlerName);
                return client;
            }
            catch (InvalidOperationException)
            {
                // Re-throw configuration-related exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create CopilotClient. Please verify configuration and authentication settings.");
                throw new InvalidOperationException("Failed to create CopilotClient. See inner exception for details.", ex);
            }
        }
    }
}
