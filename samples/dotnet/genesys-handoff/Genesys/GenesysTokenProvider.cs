// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Shared token provider for Genesys Cloud OAuth client credentials authentication.
    /// Caches the token and refreshes it automatically when it expires.
    /// Registered as a singleton and shared by <see cref="GenesysMessageSender"/> and <see cref="GenesysNotificationService"/>.
    /// </summary>
    public class GenesysTokenProvider : IDisposable
    {
        private readonly IGenesysConnectionSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GenesysTokenProvider> _logger;

        private string? _cachedToken;
        private DateTime _tokenExpiryTime;
        private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

        public GenesysTokenProvider(IGenesysConnectionSettings settings, IHttpClientFactory httpClientFactory, ILogger<GenesysTokenProvider> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns a valid Genesys Cloud OAuth token, refreshing it if necessary.
        /// </summary>
        public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            await _tokenSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTime)
                {
                    return _cachedToken;
                }

                using var client = _httpClientFactory.CreateClient();
                var credentialString = $"{_settings.ClientId}:{_settings.ClientSecret}";
                var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialString));

                using var request = new HttpRequestMessage(HttpMethod.Post, _settings.OauthUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
                request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Genesys OAuth token request failed. Status: {StatusCode}. Response: {Response}", response.StatusCode, errorBody);
                    response.EnsureSuccessStatusCode();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);

                var token = doc.RootElement.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("Genesys authentication token is missing from response.");

                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement)
                    ? expiresElement.GetInt32()
                    : 3600;

                _cachedToken = token;
                _tokenExpiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                return token;
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        public void Dispose()
        {
            _tokenSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
