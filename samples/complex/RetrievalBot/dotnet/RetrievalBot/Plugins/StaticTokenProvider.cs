using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Kiota.Abstractions.Authentication;

namespace RetrievalBot.Plugins;

public class StaticTokenProvider(string token) : IAccessTokenProvider
{
    public AllowedHostsValidator AllowedHostsValidator => new(["graph.microsoft.com"]);

    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        return AllowedHostsValidator.AllowedHosts.Contains(uri.Host) ? Task.FromResult(token) : Task.FromResult(string.Empty);
    }
}