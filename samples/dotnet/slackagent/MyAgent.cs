// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlackAgent;

[Agent(name: "MyAgent", description: "Demonstrates slack functionality", version: "1.0")]
[SlackExtension]
public partial class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        SlackExtension.OnMessage("-stream", OnSlackStreamMessageAsync);
        SlackExtension.OnMessage("-buttons", OnSlackButtonsAsync);
        SlackExtension.OnMessage(OnSlackMessageAsync, rank: RouteRank.Last);
        SlackExtension.OnEvent(OnSlackEventAsync, rank: RouteRank.Last);
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    // Demonstrates using the Slack API to reply to a message with the text "You said: {message text}" instead of
    // the typical ITurnContext.SendActivityAsync response.
    private async Task OnSlackMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();

        var message = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "text": "You said: {{turnContext.Activity.Text}}"
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }

    private async Task OnSlackEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();

        var message = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "text": "Agent got: {{turnContext.Activity.Name}}"
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }

    private async Task OnSlackButtonsAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        var buttons = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "thread_ts": "{{channelData.ThreadTs}}",
            "blocks": [
                {
                    "type": "section",
                    "text": { "type": "mrkdwn", "text": "Pick an option:" },
                },
                {
                    "type": "actions",
                    "elements": [
                        {
                            "type": "button",
                            "text": { "type": "plain_text", "text": "Yes" },
                            "action_id": "button_yes",
                            "value": "yes",
                        },
                        {
                            "type": "button",
                            "text": { "type": "plain_text", "text": "No" },
                            "action_id": "button_no",
                            "value": "no",
                        },
                    ],
                },
            ],
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", buttons, channelData.ApiToken, cancellationToken);
    }

    private async Task OnSlackStreamMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var stream = await SlackExtension.CreateStreamAsync(turnContext);

        try
        {
            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Working it", status: SlackTaskStatus.InProgress));
            await Task.Delay(2000, cancellationToken);

            await stream.AppendAsync(markdown_text: "This ");
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync([
                    new MarkdownTextChunk("is "),
                    new TaskUpdateChunk(id: "task1", title: "Still working it", status: SlackTaskStatus.InProgress)
                ]);
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync(markdown_text: "a ");
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync(markdown_text: "test.");

            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Done", status: SlackTaskStatus.Complete));
        }
        catch (Exception)
        {
            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Error", status: SlackTaskStatus.Error));
        }
        finally
        {
            var feedbackButtons = """
            {
                "blocks": 
                [
                    {
                        "type": "context_actions",
                        "elements": [
                            {
                                "type": "feedback_buttons",
                                "action_id": "feedback",
                                "positive_button": {
                                    "text": {
                                        "type": "plain_text",
                                        "text": "👍"
                                    },
                                    "value": "positive_feedback"
                                },
                                "negative_button": {
                                    "text": {
                                        "type": "plain_text",
                                        "text": "👎"
                                    },
                                    "value": "negative_feedback"
                                }
                            }
                        ]
                    }
                ]
            }
            """;

            // Legacy: https://docs.slack.dev/legacy/legacy-messaging/legacy-message-buttons/
            // New: Feedback buttons: https://docs.slack.dev/reference/block-kit/blocks/context-actions-block
            await stream.StopAsync(blocks: feedbackButtons);
        }
    }
}