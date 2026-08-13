// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Configures the named "mcs" HttpClient used by CopilotClientFactory with
// keep-alive pings and appropriate handler lifetime to prevent idle-intermediary
// TCP resets during long Copilot Studio SSE streaming reads.

using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace GenesysHandoff.Services
{
    public static class McsHttpClientRegistration
    {
        private const string McsHandlerName = "mcs";

        public static IServiceCollection AddResilientMcsHttpClient(this IServiceCollection services)
        {
            // Preserve the default unnamed client used elsewhere in the app.
            services.AddHttpClient();

            services.AddHttpClient(McsHandlerName)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

                    // Keep the outbound socket alive DURING an active SSE request.
                    // Prevents idle-intermediary RSTs on long generative turns.
                    KeepAlivePingDelay = TimeSpan.FromSeconds(15),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
                    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,

                    EnableMultipleHttp2Connections = true,
                    ConnectTimeout = TimeSpan.FromSeconds(15),
                })
                // Do NOT let IHttpClientFactory recycle the handler mid-stream.
                // Connection lifetime is managed by PooledConnectionLifetime above.
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            return services;
        }
    }
}
