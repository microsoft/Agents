// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RetrievalAgent.Services;

public sealed class BuildRetrievalService : IBuildRetrievalService
{
    private readonly HttpClient _httpClient;
    private readonly RetrievalOptions _options;
    private readonly ILogger<BuildRetrievalService> _logger;

    public BuildRetrievalService(HttpClient httpClient, IOptions<RetrievalOptions> options, ILogger<BuildRetrievalService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(string question, Func<CancellationToken, Task<string>> getAccessTokenAsync, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return RetrievalResult.Failure(RetrievalStatus.NoResults);
        }

        string accessToken;
        try
        {
            accessToken = await getAccessTokenAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The delegated Microsoft Graph token was not available.");
            return RetrievalResult.Failure(RetrievalStatus.NotSignedIn);
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RetrievalResult.Failure(RetrievalStatus.NotSignedIn);
        }

        using HttpRequestMessage request = new(HttpMethod.Post, "copilot/retrieval")
        {
            Content = JsonContent.Create(new
            {
                queryString = question,
                // SharePoint is the default data source. See the README for the OneDrive for Business fallback.
                dataSource = "sharePoint",
                filterExpression = RetrievalOptions.CreateFilterExpression(_options.SharePointSiteUrl),
                resourceMetadata = new[] { "title", "author" },
                maximumNumberOfResults = _options.MaximumNumberOfResults,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Copilot Retrieval API returned status code {StatusCode}.", (int)response.StatusCode);
                return RetrievalResult.Failure(RetrievalStatus.ServiceUnavailable);
            }

            using JsonDocument payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            IReadOnlyList<RetrievalItem> items = MapItems(payload.RootElement);
            return items.Count == 0
                ? RetrievalResult.Failure(RetrievalStatus.NoResults)
                : RetrievalResult.Success(items);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Copilot Retrieval API returned an invalid response.");
            return RetrievalResult.Failure(RetrievalStatus.ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Copilot Retrieval API request failed.");
            return RetrievalResult.Failure(RetrievalStatus.ServiceUnavailable);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Copilot Retrieval API processing failed.");
            return RetrievalResult.Failure(RetrievalStatus.ServiceUnavailable);
        }
    }

    private static IReadOnlyList<RetrievalItem> MapItems(JsonElement payload)
    {
        if (!payload.TryGetProperty("retrievalHits", out JsonElement hits) || hits.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RetrievalItem>();
        }

        List<RetrievalItem> items = [];
        foreach (JsonElement hit in hits.EnumerateArray())
        {
            string? sourceUrl = GetString(hit, "webUrl");
            string? extract = GetFirstExtract(hit);
            if (string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(extract))
            {
                continue;
            }

            string title = GetString(hit, "resourceMetadata", "title") ?? "Build session information";
            items.Add(new RetrievalItem(title, extract, sourceUrl));
        }

        return items;
    }

    private static string? GetFirstExtract(JsonElement hit)
    {
        if (!hit.TryGetProperty("extracts", out JsonElement extracts) || extracts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return extracts.EnumerateArray()
            .Select(extract => GetString(extract, "text"))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        foreach (string property in path)
        {
            if (!element.TryGetProperty(property, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
