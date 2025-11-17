// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.Extensions.Configuration;

namespace GenesysHandoff.Genesys
{

    /// <summary>
    /// Represents the configuration settings required to establish a connection to the Genesys API.
    /// </summary>
    /// <remarks>This class encapsulates the OAuth URL, API URL, integration ID, client ID, and client secret
    /// necessary for authenticating and interacting with the Genesys platform. The settings are typically loaded from a
    /// configuration section.</remarks>
    internal class GenesysConnectionSetting : IGenesysConnectionSettings
    {
        /// <summary>
        /// Gets or sets the OAuth URL used for authentication.
        /// </summary>
        public string? OauthUrl { get; set; }

        /// <summary>
        /// Gets or sets the base URL of the API used for making requests.
        /// </summary>
        public string? ApiUrl { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the integration.
        /// </summary>
        public string? IntegrationId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the client.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret used for authentication with the external service.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysConnectionSetting"/> class.
        /// </summary>
        public GenesysConnectionSetting() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysConnectionSetting"/> class using the specified configuration
        /// section.
        /// </summary>
        /// <remarks>This constructor reads the specified configuration section and initializes the properties of
        /// the <see cref="GenesysConnectionSetting"/> instance. If the provided configuration section is null or does not
        /// exist, the properties will not be initialized.</remarks>
        /// <param name="config">The configuration section containing the settings for the Genesys connection.  The section must include values
        /// for "OauthUrl", "ApiUrl", "IntegrationId", "ClientId", and "ClientSecret".</param>
        public GenesysConnectionSetting(IConfigurationSection config)
        {
            if (config != null && config.Exists())
            {
                OauthUrl = config.GetValue<string>("OauthUrl");
                ApiUrl = config.GetValue<string>("ApiUrl");
                IntegrationId = config.GetValue<string>("IntegrationId");
                ClientId = config.GetValue<string>("ClientId");
                ClientSecret = config.GetValue<string>("ClientSecret");
            }
        }
    }
}