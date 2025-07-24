// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client;

namespace CopilotStudioClientSample
{
    /// <summary>
    /// This sample uses an HttpClientHandler to add an authentication token to the request.
    /// In this case its using client secret for the request. 
    /// </summary>
    /// <param name="settings">Direct To engine connection settings.</param>
    internal class AddTokenHandlerS2S(SampleConnectionSettings settings) : DelegatingHandler(new HttpClientHandler())
    {
        private static readonly string _keyChainServiceName = "copilot_studio_client_app";
        private static readonly string _keyChainAccountName = "copilot_studio_client";

        private IConfidentialClientApplication? _confidentialClientApplication;
        private string[]? _scopes;

        private async Task<AuthenticationResult> AuthenticateAsync(CancellationToken ct = default!)
        {
            if (_confidentialClientApplication == null)
            {
                ArgumentNullException.ThrowIfNull(settings);
                _scopes = [CopilotClient.ScopeFromSettings(settings)];
                _confidentialClientApplication = ConfidentialClientApplicationBuilder.Create(settings.AppClientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, settings.TenantId)
                    .WithClientSecret(settings.AppClientSecret)
                    .Build();

                string currentDir = Path.Combine(AppContext.BaseDirectory, "mcs_client_console");

                if (!Directory.Exists(currentDir))
                {
                    Directory.CreateDirectory(currentDir);
                }

                StorageCreationPropertiesBuilder storageProperties = new("AppTokenCache", currentDir);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    storageProperties.WithLinuxUnprotectedFile();
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    storageProperties.WithMacKeyChain(_keyChainServiceName, _keyChainAccountName);
                }
                MsalCacheHelper tokenCacheHelper = await MsalCacheHelper.CreateAsync(storageProperties.Build());
                tokenCacheHelper.RegisterCache(_confidentialClientApplication.AppTokenCache);
            }

            AuthenticationResult authResponse;
            authResponse = await _confidentialClientApplication.AcquireTokenForClient(_scopes).ExecuteAsync(ct);
            return authResponse;
        }

        /// <summary>
        /// Handles sending the request and adding the token to the request.
        /// </summary>
        /// <param name="request">Request to be sent</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                AuthenticationResult authResponse = await AuthenticateAsync(cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
