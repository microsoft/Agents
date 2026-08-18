// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Channels;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Slack.Api;

namespace AgentFrameworkWeather.Services;

public sealed record SlackWorkItem(
    IChannelAdapter Adapter,
    string ConversationId,
    string UserText,
    SlackChannelData ChannelData);

public sealed class SlackWorkQueue
{
    private readonly Channel<SlackWorkItem> _items = Channel.CreateUnbounded<SlackWorkItem>();

    public ChannelReader<SlackWorkItem> Reader => _items.Reader;

    public bool TryEnqueue(SlackWorkItem item) => _items.Writer.TryWrite(item);
}