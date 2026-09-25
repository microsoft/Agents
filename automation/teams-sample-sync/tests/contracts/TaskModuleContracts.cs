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
}
