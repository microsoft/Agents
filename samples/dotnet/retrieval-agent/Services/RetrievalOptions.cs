// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace RetrievalAgent.Services;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public string SharePointSiteUrl { get; set; } = string.Empty;

    public int MaximumNumberOfResults { get; set; } = 3;

    public static bool IsValidSiteUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase);

    public static string CreateFilterExpression(string siteUrl)
    {
        Uri siteUri = new(siteUrl, UriKind.Absolute);
        return $"path:\"{siteUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/\"";
    }
}
