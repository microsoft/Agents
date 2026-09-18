// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentTargetedMessages;

public sealed class ReminderService : BackgroundService
{
    private readonly ConcurrentDictionary<string, ReminderInfo> _reminders = new();
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(ILogger<ReminderService> logger)
    {
        _logger = logger;
    }

    public async Task<ReminderInfo> AddAsync(
        string id,
        Proactive proactive,
        ITurnContext turnContext,
        ChannelAccount creator,
        ChannelAccount target,
        string reminderText,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        Conversation conversation = new(turnContext);
        await proactive.StoreConversationAsync(conversation, cancellationToken);

        ReminderInfo reminder = new(
            id,
            proactive,
            conversation,
            turnContext.Adapter,
            creator,
            target,
            reminderText,
            DateTimeOffset.UtcNow.Add(delay).ToUnixTimeMilliseconds());
        _reminders[id] = reminder;
        return reminder;
    }

    public bool Cancel(string id) => _reminders.TryRemove(id, out _);

    public bool TryGet(string id, out ReminderInfo? reminder) => _reminders.TryGetValue(id, out reminder);

    public IReadOnlyList<ReminderInfo> GetForUser(string userId)
    {
        return _reminders.Values
            .Where(reminder => reminder.Target.Id == userId || reminder.Creator.Id == userId)
            .OrderBy(reminder => reminder.DueTime)
            .ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (ReminderInfo reminder in _reminders.Values.Where(item => item.DueTime <= now))
            {
                if (_reminders.TryRemove(reminder.Id, out _))
                {
                    try
                    {
                        await DeliverAsync(reminder, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reminder delivery failed for {ReminderId}.", reminder.Id);
                    }
                }
            }
        }
    }

    private static async Task DeliverAsync(ReminderInfo reminder, CancellationToken cancellationToken)
    {
        await reminder.Proactive.ContinueConversationAsync(
            reminder.Adapter,
            reminder.Conversation,
            async (turnContext, turnState, ct) =>
            {
                IActivity activity = AgentTargetedMessagesAgent.CreateCardActivity(
                    AgentTargetedMessagesAgent.CreateDeliveryCard(reminder));
                activity.MakeTargetedActivity(reminder.Target);
                await turnContext.SendActivityAsync(activity, ct);
            },
            cancellationToken: cancellationToken);
    }
}

public sealed record ReminderInfo(
    string Id,
    Proactive Proactive,
    Conversation Conversation,
    IChannelAdapter Adapter,
    ChannelAccount Creator,
    ChannelAccount Target,
    string ReminderText,
    long DueTime);
