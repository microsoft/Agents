namespace GenesysHandoff.Genesys
{
    public interface IGenesysConnectionSettings
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
    }
}
