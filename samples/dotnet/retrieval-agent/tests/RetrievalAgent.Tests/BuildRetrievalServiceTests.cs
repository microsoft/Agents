using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Agents.Core.Models;
using RetrievalAgent.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetrievalAgent.Tests;

public class BuildRetrievalServiceTests
{
    [Fact]
    public void SiteUrlCreatesConfiguredPathFilter()
    {
        string filter = RetrievalOptions.CreateFilterExpression("https://contoso.sharepoint.com/sites/Build");

        Assert.Equal("path:\"https://contoso.sharepoint.com/sites/Build/\"", filter);
        Assert.True(RetrievalOptions.IsValidSiteUrl("https://contoso.sharepoint.com/sites/Build"));
        Assert.False(RetrievalOptions.IsValidSiteUrl("http://contoso.sharepoint.com/sites/Build"));
    }

    [Fact]
    public async Task RetrievesItemsUsingDelegatedTokenAndConfiguredSite()
    {
        StubHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                { "retrievalHits": [{ "webUrl": "https://contoso.sharepoint.com/sites/Build/session.docx", "extracts": [{ "text": "Pricing Analytics is a Build session." }], "resourceMetadata": { "title": "Pricing Analytics" } }] }
                """, Encoding.UTF8, "application/json"),
        });
        BuildRetrievalService service = CreateService(handler);

        RetrievalResult result = await service.RetrieveAsync("Tell me about Pricing Analytics", _ => Task.FromResult("delegated-token"), CancellationToken.None);

        Assert.Equal(RetrievalStatus.Success, result.Status);
        Assert.Single(result.Items);
        Assert.Equal("Pricing Analytics", result.Items[0].Title);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("delegated-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("https://graph.microsoft.com/v1.0/copilot/retrieval", handler.Request.RequestUri!.ToString());
        Assert.Contains("\"dataSource\":\"sharePoint\"", handler.RequestBody);
        Assert.Contains("https://contoso.sharepoint.com/sites/Build/", handler.RequestBody);
    }

    [Fact]
    public async Task ReturnsNoResultsWhenRetrievalHasNoHits()
    {
        StubHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ \"retrievalHits\": [] }", Encoding.UTF8, "application/json"),
        });

        RetrievalResult result = await CreateService(handler).RetrieveAsync("Build sessions", _ => Task.FromResult("token"), CancellationToken.None);

        Assert.Equal(RetrievalStatus.NoResults, result.Status);
        Assert.Equal(BuildGenieResponses.For(RetrievalStatus.NoResults), BuildGenieResponses.For(result.Status));
    }

    [Fact]
    public async Task ReturnsSafeSignInFailureWhenTokenIsUnavailable()
    {
        RetrievalResult result = await CreateService(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK))).RetrieveAsync(
            "Build sessions",
            _ => throw new InvalidOperationException("token details must not reach chat"),
            CancellationToken.None);

        Assert.Equal(RetrievalStatus.NotSignedIn, result.Status);
        Assert.DoesNotContain("token details", BuildGenieResponses.For(result.Status), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnsSafeServiceFailureWhenGraphFails()
    {
        RetrievalResult result = await CreateService(new StubHandler(new HttpResponseMessage(HttpStatusCode.BadGateway))).RetrieveAsync(
            "Build sessions",
            _ => Task.FromResult("token"),
            CancellationToken.None);

        Assert.Equal(RetrievalStatus.ServiceUnavailable, result.Status);
        Assert.DoesNotContain("BadGateway", BuildGenieResponses.For(result.Status), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GroundedAnswerIncludesTheRetrievedSourceLink()
    {
        string response = BuildGenieResponses.GroundedAnswer([new RetrievalItem("Pricing Analytics", "Session details", "https://contoso.sharepoint.com/session.docx")]);

        Assert.Contains("Session details", response);
        Assert.Contains("https://contoso.sharepoint.com/session.docx", response);
    }

    [Fact]
    public async Task MessageRouteSendsGroundedTextAndSourceCardForSuccessfulRetrieval()
    {
        FakeRetrievalService retrieval = new(RetrievalResult.Success([new RetrievalItem("Pricing Analytics", "Session details", "https://contoso.sharepoint.com/session.docx")]));
        BuildGenieMessageRoute route = new(retrieval);
        List<string> messages = [];
        List<IActivity> activities = [];

        await route.HandleAsync("Pricing Analytics", _ => Task.FromResult("token"), (text, _) => { messages.Add(text); return Task.CompletedTask; }, (activity, _) => { activities.Add(activity); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal("Pricing Analytics", retrieval.Question);
        Assert.Single(messages);
        Assert.Contains("Session details", messages[0]);
        Assert.Contains("https://contoso.sharepoint.com/session.docx", messages[0]);
        Assert.Single(activities);
        Assert.NotNull(activities[0].Attachments);
        Assert.Single(activities[0].Attachments);
        Assert.Equal("application/vnd.microsoft.card.adaptive", activities[0].Attachments[0].ContentType);
    }

    [Theory]
    [InlineData(RetrievalStatus.NotSignedIn)]
    [InlineData(RetrievalStatus.NoResults)]
    [InlineData(RetrievalStatus.ServiceUnavailable)]
    public async Task MessageRouteSendsSafeMessageForFailedRetrieval(RetrievalStatus status)
    {
        BuildGenieMessageRoute route = new(new FakeRetrievalService(RetrievalResult.Failure(status)));
        List<string> messages = [];
        List<IActivity> activities = [];

        await route.HandleAsync("Build sessions", _ => Task.FromResult("token"), (text, _) => { messages.Add(text); return Task.CompletedTask; }, (activity, _) => { activities.Add(activity); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal([BuildGenieResponses.For(status)], messages);
        Assert.Empty(activities);
    }

    private static BuildRetrievalService CreateService(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") },
        Options.Create(new RetrievalOptions { SharePointSiteUrl = "https://contoso.sharepoint.com/sites/Build" }),
        NullLogger<BuildRetrievalService>.Instance);

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class FakeRetrievalService(RetrievalResult result) : IBuildRetrievalService
    {
        public string? Question { get; private set; }

        public Task<RetrievalResult> RetrieveAsync(string question, Func<CancellationToken, Task<string>> getAccessTokenAsync, CancellationToken cancellationToken)
        {
            Question = question;
            return Task.FromResult(result);
        }
    }
}
