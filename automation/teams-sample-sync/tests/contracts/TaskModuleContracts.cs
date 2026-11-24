// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Teams.Samples.BotTaskModules;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "bot-task-modules")]
    public async Task TaskFetch_ReturnsConfiguredCustomFormDialogAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BotEndpoint"] = "https://example.test" }).Build();
        await using var host = CreateHost(services => new BotTaskModulesAgent(CreateOptions(services), configuration));
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "task/fetch",
                Value = new { data = new { data = "CustomForm" } } })
            .AssertReply(activity =>
            {
                Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
                var response = Payload(activity.Value);
                Assert.Equal(200, response["status"]!.Value<int>());
                var dialog = response["body"]!["task"]!;
                Assert.Equal("continue", dialog["type"]!.Value<string>());
                Assert.Equal("https://example.test/customform", dialog["value"]!["url"]!.Value<string>());
                Assert.Equal("https://example.test/customform", dialog["value"]!["fallbackUrl"]!.Value<string>());
            }).AssertNoMoreReplies().StartTestAsync();
    }
    [Theory]
    [Trait("Sample", "bot-task-modules")]
    [InlineData("AdaptiveCard", "usertext")]
    [InlineData("MultiStep", "name")]
    public async Task TaskFetch_ReturnsAdaptiveInputsAsync(string route, string inputId)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BotEndpoint"] = "https://example.test" }).Build();
        await using var host = CreateHost(services => new BotTaskModulesAgent(CreateOptions(services), configuration));
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "task/fetch",
                Value = new { data = new { data = route } } })
            .AssertReply(activity =>
            {
                var task = Payload(activity.Value)["body"]!["task"]!;
                Assert.Equal("continue", task["type"]!.Value<string>());
                Assert.Equal(ContentTypes.AdaptiveCard, task["value"]!["card"]!["contentType"]!.Value<string>());
                Assert.Contains(task["value"]!["card"]!["content"]!["body"]!.Children(),
                    input => input["id"]?.Value<string>() == inputId);
            }).AssertNoMoreReplies().StartTestAsync();
    }

    [Fact]
    [Trait("Sample", "bot-task-modules")]
    public async Task TaskSubmit_ChainsNameIntoEmailStepAndCompletesAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BotEndpoint"] = "https://example.test" }).Build();
        await using var host = CreateHost(services => new BotTaskModulesAgent(CreateOptions(services), configuration));
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "task/submit",
                Value = new { data = new { submissiontype = "multi_step_1", name = "Ada" } } })
            .AssertReply(activity =>
            {
                var card = Payload(activity.Value)["body"]!["task"]!["value"]!["card"]!["content"]!;
                Assert.Equal("Ada", card["actions"]![0]!["data"]!["name"]!.Value<string>());
            })
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "task/submit",
                Value = new { data = new { submissiontype = "multi_step_2", name = "Ada", email = "ada@example.test" } } })
            .AssertReplyContains("Hi Ada, thanks for submitting! Your email is ada@example.test")
            .AssertReply(activity =>
            {
                var task = Payload(activity.Value)["body"]!["task"]!;
                Assert.Equal("message", task["type"]!.Value<string>());
                Assert.Equal("Multi-step form completed!", task["value"]!.Value<string>());
            }).AssertNoMoreReplies().StartTestAsync();
    }

}
