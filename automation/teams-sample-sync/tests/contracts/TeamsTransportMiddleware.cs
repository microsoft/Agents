// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;

namespace TeamsSampleSync.ContractTests;

// TestAdapter captures replies but does not install the REST transports that
// CloudAdapter supplies. Teams SDK 1.9 requires both even when no API is called.
internal sealed class TeamsTransportMiddleware(IHttpClientFactory? httpClients = null) : IMiddleware
{
    private static readonly IHttpClientFactory HttpClients = new OfflineHttpClientFactory();

    public async Task OnTurnAsync(ITurnContext context, NextDelegate next, CancellationToken cancellationToken)
    {
        using var connector = new RestConnectorClient(
            new Uri("https://connector.invalid/"), httpClients ?? HttpClients, () => Task.FromResult("test-token"));
        using var userToken = new RestUserTokenClient(
            "contract-agent", new Uri("https://token.invalid/"), HttpClients, () => Task.FromResult("test-token"));
        context.Services.Set<IConnectorClient>(connector);
        context.Services.Set<IUserTokenClient>(userToken);

        await next(cancellationToken);
    }

    private sealed class OfflineHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new RejectNetworkRequests());
        }
    }

    private sealed class RejectNetworkRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                $"Unexpected Teams API request in an offline contract: {request.Method} {request.RequestUri}");
        }
    }
}
