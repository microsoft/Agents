// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsSampleSync.ContractTests;

internal sealed class ContractHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new Handler(respond));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
