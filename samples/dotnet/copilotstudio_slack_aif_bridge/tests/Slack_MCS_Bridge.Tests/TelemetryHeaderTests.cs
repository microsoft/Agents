using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Slack_MCS_Bridge.Tests;

public class TelemetryHeaderTests
{
    private const string TagName = "http.request.headers";

    public static IEnumerable<object[]> SensitiveHeaders()
    {
        string[] names =
        [
            "Authorization", "Proxy-Authorization", "api-key", "x-api-key",
            "Ocp-Apim-Subscription-Key", "x-functions-key", "Cookie", "Set-Cookie"
        ];

        foreach (var name in names)
        {
            foreach (var casing in new[] { name, name.ToLowerInvariant(), name.ToUpperInvariant() }.Distinct())
            {
                yield return [casing, false];
                yield return [casing, true];
            }
        }
    }

    [Theory]
    [MemberData(nameof(SensitiveHeaders))]
    public void ExcludesSensitiveHeadersRegardlessOfCasing(string name, bool aspNetHeaders)
    {
        using var activity = new Activity("header-test");
        if (aspNetHeaders)
        {
            var headers = new HeaderDictionary { [name] = "test-secret", ["X-Request-ID"] = "request-123" };
            Extract(activity, headers, typeof(IHeaderDictionary));
        }
        else
        {
            using var request = new HttpRequestMessage();
            using var response = new HttpResponseMessage();
            HttpHeaders headers = name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? response.Headers
                : request.Headers;
            Assert.True(headers.TryAddWithoutValidation(name, "test-secret"));
            Assert.True(headers.TryAddWithoutValidation("X-Request-ID", "request-123"));
            Extract(activity, headers, typeof(HttpHeaders));
        }

        var captured = Assert.IsType<string[]>(activity.GetTagItem(TagName));
        Assert.Equal("X-Request-ID=request-123", Assert.Single(captured));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesNonSensitiveHeadersAndMultipleValues(bool aspNetHeaders)
    {
        using var activity = new Activity("header-test");
        if (aspNetHeaders)
        {
            var headers = new HeaderDictionary
            {
                ["Accept"] = new[] { "application/json", "text/plain" },
                ["X-Custom-Diagnostic"] = "diagnostic-value"
            };
            Extract(activity, headers, typeof(IHeaderDictionary));
        }
        else
        {
            using var request = new HttpRequestMessage();
            request.Headers.Add("Accept", new[] { "application/json", "text/plain" });
            request.Headers.Add("X-Custom-Diagnostic", "diagnostic-value");
            Extract(activity, request.Headers, typeof(HttpHeaders));
        }

        var captured = Assert.IsType<string[]>(activity.GetTagItem(TagName));
        Assert.Equal(2, captured.Length);
        Assert.Contains("Accept=application/json,text/plain", captured);
        Assert.Contains("X-Custom-Diagnostic=diagnostic-value", captured);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OmitsTagWhenAllHeadersAreSensitive(bool aspNetHeaders)
    {
        using var activity = new Activity("header-test");
        if (aspNetHeaders)
        {
            Extract(activity, new HeaderDictionary { ["api-key"] = "test-secret" }, typeof(IHeaderDictionary));
        }
        else
        {
            using var request = new HttpRequestMessage();
            request.Headers.Add("api-key", "test-secret");
            Extract(activity, request.Headers, typeof(HttpHeaders));
        }

        Assert.Null(activity.GetTagItem(TagName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OmitsTagForNullOrEmptyHeaders(bool aspNetHeaders)
    {
        using var activity = new Activity("header-test");
        var headerType = aspNetHeaders ? typeof(IHeaderDictionary) : typeof(HttpHeaders);
        Extract(activity, null, headerType);
        Assert.Null(activity.GetTagItem(TagName));

        using var request = new HttpRequestMessage();
        Extract(activity, aspNetHeaders ? new HeaderDictionary() : request.Headers, headerType);
        Assert.Null(activity.GetTagItem(TagName));
    }

    private static void Extract(Activity activity, object? headers, Type headerType)
    {
        var method = typeof(AgentOtelExtension).GetMethod(
            "ExtractHeadersForOTEL",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(Activity), headerType, typeof(string)]);
        Assert.NotNull(method);
        method.Invoke(null, [activity, headers, TagName]);
    }
}
