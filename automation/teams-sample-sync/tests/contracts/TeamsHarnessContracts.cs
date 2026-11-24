// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Category", "TeamsHarness")]
    public async Task TeamsExtension_InitializesBeforeDispatchingAnInvokeAsync()
    {
        await using var host = CreateHost(services =>
        {
            var agent = new AgentApplication(CreateOptions(services));
            _ = new TeamsAgentExtension(agent);
            agent.OnActivity(ActivityTypes.Invoke, async (context, _, cancellationToken) =>
            {
                await context.SendActivityAsync("Invoke dispatched", cancellationToken: cancellationToken);
            });
            return agent;
        });

        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "contract/invoke" })
            .AssertReply("Invoke dispatched")
            .StartTestAsync();
    }
}
