// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using AgentTargetedMessages;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "agent-targeted-messages")]
    public async Task ReminderHelp_TargetsTheRequestingUserAsync()
    {
        using var reminders = new ReminderService(NullLogger<ReminderService>.Instance);
        await using var host = CreateHost(services => new AgentTargetedMessagesAgent(CreateOptions(services), reminders));
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Message, Text = "reminder-help",
                From = new ChannelAccount { Id = "requesting-user", Name = "Contract user" } })
            .AssertReply(activity =>
            {
                Assert.True(activity.IsTargetedActivity());
                Assert.Equal("requesting-user", activity.Recipient.Id);
                Assert.Contains("my-reminders", activity.Text);
                Assert.Contains("cancel-reminder", activity.Text);
                Assert.Contains("requesting-user", activity.SuggestedActions.To);
            }).AssertNoMoreReplies().StartTestAsync();
    }
}
