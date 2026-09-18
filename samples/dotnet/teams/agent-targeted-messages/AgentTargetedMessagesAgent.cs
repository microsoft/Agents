// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Teams.Api.Messages;
using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;
using AdaptiveCard = Microsoft.Teams.Cards.AdaptiveCard;
using TeamsAction = Microsoft.Teams.Cards.Action;

namespace AgentTargetedMessages;

[TeamsExtension]
public partial class AgentTargetedMessagesAgent(
    AgentApplicationOptions options,
    ReminderService reminderService) : AgentApplication(options)
{
    private static readonly Regex TimePattern = new(
        @"in\s+(\d+)\s*(seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h)\b",
        RegexOptions.IgnoreCase);

    [TeamsMessageRoute]
    public async Task OnMessageAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(turnContext.Activity.Text))
        {
            return;
        }

        bool isTargeted = turnContext.Activity.IsTargetedActivity();
        string text = turnContext.Activity.RemoveRecipientMention().Trim();
        string lower = text.ToLowerInvariant();

        if (lower is "reminder-help" or "help")
        {
            await ShowHelpAsync(turnContext, cancellationToken);
        }
        else if (lower.StartsWith("remind", StringComparison.Ordinal))
        {
            await HandleRemindCommandAsync(turnContext, text, isTargeted, cancellationToken);
        }
        else if (lower == "my-reminders")
        {
            await ShowMyRemindersAsync(turnContext, isTargeted, cancellationToken);
        }
        else if (lower.StartsWith("cancel-reminder", StringComparison.Ordinal))
        {
            string reminderId = Regex.Replace(
                text,
                @"^cancel-reminder\s*",
                string.Empty,
                RegexOptions.IgnoreCase).Trim();
            await CancelReminderAsync(turnContext, reminderId, isTargeted, cancellationToken);
        }
        else if (lower.StartsWith("add-reaction", StringComparison.Ordinal))
        {
            await HandleAddReactionAsync(turnContext, text, cancellationToken);
        }
        else if (lower.StartsWith("remove-reaction", StringComparison.Ordinal))
        {
            await HandleRemoveReactionAsync(turnContext, text, cancellationToken);
        }
        else
        {
            IActivity response = CreateTextActivity("Use `reminder-help` to see available commands.");
            response.SuggestedActions = BuildSuggestedCommands(
                turnContext.Activity.From?.Id,
                ("Show help", "reminder-help"),
                ("Remind me in 5 minutes test", "remind me in 5 minutes test"),
                ("My reminders", "my-reminders"));
            response.Entities = [new AIEntity { AdditionalType = [AIEntity.AdditionalTypeAIGeneratedContent] }];

            await SendAsync(turnContext, response, isTargeted, turnContext.Activity.From, cancellationToken);
        }
    }

    [TeamsActivityRoute(ActivityTypes.Invoke, rank: RouteRank.Last)]
    public async Task OnSuggestedActionSubmitAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(turnContext.Activity.Name, "suggestedActions/submit", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? command = GetJsonString(turnContext.Activity.Value, "command");
        if (string.IsNullOrEmpty(command))
        {
            await turnContext.SendActivityAsync("No command specified.", cancellationToken: cancellationToken);
            return;
        }

        string lower = command.ToLowerInvariant();
        if (lower is "reminder-help" or "help")
        {
            await ShowHelpAsync(turnContext, cancellationToken);
        }
        else if (lower.StartsWith("remind", StringComparison.Ordinal))
        {
            await HandleRemindCommandAsync(turnContext, command, false, cancellationToken);
        }
        else if (lower == "my-reminders")
        {
            await ShowMyRemindersAsync(turnContext, false, cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync($"Executing: {command}", cancellationToken: cancellationToken);
        }
    }

    [ActionExecuteRoute("cancel_reminder")]
    public Task<AdaptiveCardInvokeResponse> OnCancelReminderActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        string reminderId = GetJsonString(data, "reminderId") ?? string.Empty;
        if (TryCancelOwnedReminder(reminderId, turnContext.Activity.From?.Id))
        {
            return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("Reminder cancelled!"));
        }

        return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("Reminder not found or already completed."));
    }

    [ActionExecuteRoute("dismiss_reminder")]
    public Task<AdaptiveCardInvokeResponse> OnDismissReminderActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        string reminderId = GetJsonString(data, "reminderId") ?? string.Empty;
        if (TryCancelOwnedReminder(reminderId, turnContext.Activity.From?.Id))
        {
            return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("Reminder dismissed!"));
        }

        return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("Reminder not found or already completed."));
    }

    [ActionExecuteRoute("snooze_reminder")]
    public async Task<AdaptiveCardInvokeResponse> OnSnoozeReminderActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        string reminderText = GetJsonString(data, "reminderText") ?? "Snoozed reminder";
        int snoozeMinutes = int.TryParse(GetJsonString(data, "snoozeMinutes"), out int parsedMinutes)
            ? parsedMinutes
            : 5;
        string id = CreateReminderId();
        ChannelAccount sender = CopyAccount(turnContext.Activity.From);
        ReminderInfo reminder = await reminderService.AddAsync(
            id,
            Proactive,
            turnContext,
            sender,
            sender,
            reminderText,
            TimeSpan.FromMinutes(snoozeMinutes),
            cancellationToken);

        IActivity response = CreateCardActivity(
            CreateSnoozeConfirmationCard(reminder, snoozeMinutes),
            $"Snoozed for {snoozeMinutes} minutes");
        response.MakeTargetedActivity(sender);
        await turnContext.SendActivityAsync(response, cancellationToken);

        return AdaptiveCardInvokeResponseFactory.Message($"Snoozed for {snoozeMinutes} minutes!");
    }

    [MessageReactionsAddedRoute]
    public async Task OnMessageReactionsAddedAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        foreach (MessageReaction reaction in turnContext.Activity.ReactionsAdded ?? [])
        {
            string userName = turnContext.Activity.From?.Name ?? "Someone";
            await turnContext.SendActivityAsync(
                $"Thanks for the **{reaction.Type}** reaction, {userName}!",
                cancellationToken: cancellationToken);
        }
    }

    [MessageReactionsRemovedRoute]
    public Task OnMessageReactionsRemovedAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task HandleRemindCommandAsync(
        ITeamsTurnContext turnContext,
        string commandText,
        bool isTargeted,
        CancellationToken cancellationToken)
    {
        ParsedReminder parsed = ParseReminderCommand(turnContext.Activity, commandText);
        if (parsed.Error is not null)
        {
            await turnContext.SendActivityAsync(
                $"{parsed.Error}\n\nUse `reminder-help` for usage examples.",
                cancellationToken: cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(parsed.TargetUserId))
        {
            await turnContext.SendActivityAsync(
                "Could not determine who to remind. Use `remind me` or mention someone like `remind @John`.",
                cancellationToken: cancellationToken);
            return;
        }

        ChannelAccount creator = CopyAccount(turnContext.Activity.From);
        ChannelAccount target = new(parsed.TargetUserId, parsed.TargetUserName);
        string reminderId = CreateReminderId();
        ReminderInfo reminder = await reminderService.AddAsync(
            reminderId,
            Proactive,
            turnContext,
            creator,
            target,
            parsed.ReminderText!,
            TimeSpan.FromMilliseconds(parsed.DelayMs),
            cancellationToken);

        IActivity response = CreateCardActivity(CreateConfirmationCard(reminder, parsed.DelayMs));
        await SendAsync(turnContext, response, isTargeted: true, creator, cancellationToken);
    }

    private async Task ShowMyRemindersAsync(
        ITeamsTurnContext turnContext,
        bool isTargeted,
        CancellationToken cancellationToken)
    {
        string? userId = turnContext.Activity.From?.Id;
        if (string.IsNullOrEmpty(userId))
        {
            await turnContext.SendActivityAsync("Could not determine your user ID.", cancellationToken: cancellationToken);
            return;
        }

        IReadOnlyList<ReminderInfo> reminders = reminderService.GetForUser(userId);
        IActivity response;
        if (reminders.Count == 0)
        {
            response = CreateTextActivity("You have no active reminders.");
            response.SuggestedActions = BuildSuggestedCommands(
                userId,
                ("Remind me in 5 minutes test", "remind me in 5 minutes test"),
                ("Remind me in 1 hour meeting", "remind me in 1 hour meeting"),
                ("Show help", "reminder-help"));
        }
        else
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            IEnumerable<string> lines = reminders.Select(reminder =>
            {
                long timeLeft = reminder.DueTime - now;
                string time = timeLeft > 0 ? $"in {FormatTimeSpan((int)timeLeft)}" : "overdue";
                string target = reminder.Creator.Id == reminder.Target.Id ? "yourself" : reminder.Target.Name;
                return $"- **{reminder.Id}**: \"{reminder.ReminderText}\" for {target} ({time})";
            });
            response = CreateTextActivity(
                $"**Your Active Reminders:**\n\n{string.Join("\n", lines)}\n\n" +
                "Use `cancel-reminder [id]` to cancel a reminder.");
        }

        await SendAsync(turnContext, response, isTargeted, turnContext.Activity.From, cancellationToken);
    }

    private async Task CancelReminderAsync(
        ITeamsTurnContext turnContext,
        string reminderId,
        bool isTargeted,
        CancellationToken cancellationToken)
    {
        string? userId = turnContext.Activity.From?.Id;
        string message;
        if (string.IsNullOrEmpty(reminderId))
        {
            message = "Please specify a reminder ID. Use `my-reminders` to see your active reminders.";
        }
        else if (!reminderService.TryGet(reminderId, out ReminderInfo? reminder) || reminder is null)
        {
            message = $"Reminder **{reminderId}** not found or already completed.";
        }
        else if (TryCancelOwnedReminder(reminderId, userId))
        {
            message = $"Reminder **{reminderId}** has been cancelled.";
        }
        else
        {
            message = "You can only cancel reminders you created or are assigned to you.";
        }

        await SendAsync(
            turnContext,
            CreateTextActivity(message),
            isTargeted,
            turnContext.Activity.From,
            cancellationToken);
    }

    private static async Task ShowHelpAsync(
        ITeamsTurnContext turnContext,
        CancellationToken cancellationToken)
    {
        const string helpText =
            "**Personal Reminder Bot - Help**\n\n" +
            "**Set a Reminder:**\n" +
            "- `remind me in 5 minutes to check email`\n" +
            "- `remind me in 1 hour meeting starts`\n" +
            "- `remind me in 30 seconds test`\n" +
            "**Supported Time Formats:**\n" +
            "- Seconds: `30 seconds`, `30 secs`, `30s`\n" +
            "- Minutes: `5 minutes`, `5 mins`, `5m`\n" +
            "- Hours: `1 hour`, `2 hrs`, `1h`\n\n" +
            "**Manage Reminders:**\n" +
            "- `my-reminders` - View your active reminders\n" +
            "- `cancel-reminder [id]` - Cancel a specific reminder\n" +
            "- `reminder-help` - Show this help message\n\n" +
            "**How It Works:**\n" +
            "- Reminders are delivered as **targeted messages** (only the recipient can see them)\n" +
            "- Works in both **channels** and **group chats**\n" +
            "- Set reminders for yourself or mention others\n" +
            "- Dismiss or snooze reminders via card buttons\n\n" +
            "**Reactions:**\n" +
            "- `add-reaction [type]` - Bot adds a reaction to your message\n" +
            "- `remove-reaction [type]` - Bot removes a reaction from your message\n" +
            "- React to any bot message and the bot will acknowledge it!";

        IActivity response = CreateTextActivity(helpText);
        response.SuggestedActions = BuildSuggestedCommands(
            turnContext.Activity.From?.Id,
            ("Set a 30-second test reminder", "remind me in 30 seconds test"),
            ("Set a 5-minute reminder", "remind me in 5 minutes check email"),
            ("My reminders", "my-reminders"));
        await SendAsync(turnContext, response, true, turnContext.Activity.From, cancellationToken);
    }

    private static async Task HandleAddReactionAsync(
        ITeamsTurnContext turnContext,
        string commandText,
        CancellationToken cancellationToken)
    {
        string reactionType = Regex.Replace(
            commandText,
            @"^add-reaction\s*",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrEmpty(reactionType))
        {
            await turnContext.SendActivityAsync(
                "Please specify a reaction type. Example: `add-reaction like`",
                cancellationToken: cancellationToken);
            return;
        }

        await turnContext.Client.Conversations.Reactions.AddAsync(
            turnContext.Activity.Conversation.Id,
            turnContext.Activity.Id,
            new ReactionType(reactionType),
            cancellationToken);
        await turnContext.SendActivityAsync(
            $"Added a **{reactionType}** reaction to your message!",
            cancellationToken: cancellationToken);
    }

    private static async Task HandleRemoveReactionAsync(
        ITeamsTurnContext turnContext,
        string commandText,
        CancellationToken cancellationToken)
    {
        string reactionType = Regex.Replace(
            commandText,
            @"^remove-reaction\s*",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrEmpty(reactionType))
        {
            await turnContext.SendActivityAsync(
                "Please specify a reaction type. Example: `remove-reaction like`",
                cancellationToken: cancellationToken);
            return;
        }

        await turnContext.Client.Conversations.Reactions.DeleteAsync(
            turnContext.Activity.Conversation.Id,
            turnContext.Activity.Id,
            new ReactionType(reactionType),
            cancellationToken);
        await turnContext.SendActivityAsync(
            $"Removed the **{reactionType}** reaction from your message!",
            cancellationToken: cancellationToken);
    }

    private static ParsedReminder ParseReminderCommand(IActivity activity, string commandText)
    {
        string text = commandText.Trim();
        if (text.StartsWith("remind", StringComparison.OrdinalIgnoreCase))
        {
            text = text[6..].Trim();
        }

        string targetUserId;
        string targetUserName;
        if (Regex.IsMatch(text, @"^me(\s|,|$)", RegexOptions.IgnoreCase))
        {
            targetUserId = activity.From?.Id ?? string.Empty;
            targetUserName = activity.From?.Name ?? "You";
            text = text[2..].Trim().TrimStart(',').Trim();
        }
        else
        {
            Microsoft.Agents.Core.Models.Mention? mention = activity.GetMentions()
                .FirstOrDefault(item => item.Mentioned?.Id != activity.Recipient?.Id);
            if (mention?.Mentioned is not null)
            {
                targetUserId = mention.Mentioned.Id;
                targetUserName = mention.Mentioned.Name ?? "User";
                text = text.Replace(mention.Text, string.Empty, StringComparison.Ordinal).Trim();
            }
            else
            {
                targetUserId = activity.From?.Id ?? string.Empty;
                targetUserName = activity.From?.Name ?? "You";
            }
        }

        (int DelayMs, string Label)? time = ParseTimeExpression(text);
        if (time is null)
        {
            return new ParsedReminder
            {
                Error = "Could not parse time. Use format like 'in 5 minutes', 'in 1 hour', or 'in 30 seconds'."
            };
        }

        string reminderText = TimePattern.Replace(text, string.Empty).Trim().TrimStart(',').Trim();
        if (reminderText.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
        {
            reminderText = reminderText[3..].Trim();
        }
        if (reminderText.StartsWith("that ", StringComparison.OrdinalIgnoreCase))
        {
            reminderText = reminderText[5..].Trim();
        }
        if (string.IsNullOrEmpty(reminderText))
        {
            reminderText = "You have a reminder!";
        }

        return new ParsedReminder
        {
            TargetUserId = targetUserId,
            TargetUserName = targetUserName,
            ReminderText = reminderText,
            DelayMs = time.Value.DelayMs
        };
    }

    private static (int DelayMs, string Label)? ParseTimeExpression(string text)
    {
        Match match = TimePattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        if (!long.TryParse(match.Groups[1].Value, out long value) || value <= 0)
        {
            return null;
        }
        string unit = match.Groups[2].Value.ToLowerInvariant();
        long multiplier;
        if (unit.StartsWith("second", StringComparison.Ordinal) ||
            unit.StartsWith("sec", StringComparison.Ordinal) ||
            unit == "s")
        {
            multiplier = 1000;
        }
        else if (unit.StartsWith("minute", StringComparison.Ordinal) ||
            unit.StartsWith("min", StringComparison.Ordinal) ||
            unit == "m")
        {
            multiplier = 60_000;
        }
        else if (unit.StartsWith("hour", StringComparison.Ordinal) ||
            unit.StartsWith("hr", StringComparison.Ordinal) ||
            unit == "h")
        {
            multiplier = 3_600_000;
        }

        else return null;

        if (value > int.MaxValue / multiplier) return null;
        return ((int)(value * multiplier), $"{value} {unit}");
    }

    private bool TryCancelOwnedReminder(string reminderId, string? userId) =>
        !string.IsNullOrEmpty(userId) &&
        reminderService.TryGet(reminderId, out ReminderInfo? reminder) &&
        reminder is not null &&
        (reminder.Creator.Id == userId || reminder.Target.Id == userId) &&
        reminderService.Cancel(reminderId);

    private static AdaptiveCard CreateConfirmationCard(ReminderInfo reminder, int delayMs)
    {
        string targetDisplay = reminder.Creator.Id == reminder.Target.Id ? "yourself" : reminder.Target.Name;
        return new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Reminder Set!") { Weight = TextWeight.Bolder, Size = TextSize.Medium, Color = TextColor.Good },
                new FactSet
                {
                    Facts =
                    [
                        new Microsoft.Teams.Cards.Fact("Reminder:", reminder.ReminderText),
                        new Microsoft.Teams.Cards.Fact("For:", targetDisplay),
                        new Microsoft.Teams.Cards.Fact("In:", FormatTimeSpan(delayMs)),
                        new Microsoft.Teams.Cards.Fact("ID:", reminder.Id)
                    ]
                },
                new TextBlock("This is a targeted message - only you can see this.")
                {
                    Size = TextSize.Small,
                    IsSubtle = true,
                    Wrap = true
                }
            ],
            Actions =
            [
                CreateExecuteAction("Cancel Reminder", "cancel_reminder", new Dictionary<string, object?>
                {
                    ["action"] = "cancel_reminder",
                    ["reminderId"] = reminder.Id
                })
            ]
        };
    }

    internal static AdaptiveCard CreateDeliveryCard(ReminderInfo reminder)
    {
        string fromDisplay = reminder.Creator.Id == reminder.Target.Id ? "yourself" : reminder.Creator.Name;
        return new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Reminder") { Weight = TextWeight.Bolder, Size = TextSize.Large, Color = TextColor.Accent },
                new TextBlock(reminder.ReminderText) { Wrap = true, Size = TextSize.Medium },
                new TextBlock($"Set by {fromDisplay}") { Size = TextSize.Small, IsSubtle = true }
            ],
            Actions =
            [
                CreateExecuteAction("Dismiss", "dismiss_reminder", new Dictionary<string, object?>
                {
                    ["action"] = "dismiss_reminder",
                    ["reminderId"] = reminder.Id
                }),
                CreateExecuteAction("Snooze 5 min", "snooze_reminder", new Dictionary<string, object?>
                {
                    ["action"] = "snooze_reminder",
                    ["reminderId"] = reminder.Id,
                    ["reminderText"] = reminder.ReminderText,
                    ["snoozeMinutes"] = "5"
                })
            ]
        };
    }

    private static AdaptiveCard CreateSnoozeConfirmationCard(ReminderInfo reminder, int snoozeMinutes)
    {
        return new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Snoozed!") { Weight = TextWeight.Bolder, Size = TextSize.Medium, Color = TextColor.Accent },
                new TextBlock(reminder.ReminderText) { Wrap = true },
                new TextBlock($"Will remind you again in {snoozeMinutes} minutes.")
                {
                    Size = TextSize.Small,
                    IsSubtle = true
                }
            ],
            Actions =
            [
                CreateExecuteAction("Cancel", "cancel_reminder", new Dictionary<string, object?>
                {
                    ["action"] = "cancel_reminder",
                    ["reminderId"] = reminder.Id
                })
            ]
        };
    }

    private static ExecuteAction CreateExecuteAction(
        string title,
        string verb,
        Dictionary<string, object?> data)
    {
        return new ExecuteAction
        {
            Title = title,
            Verb = verb,
            Data = new Union<string, SubmitActionData>(
                new SubmitActionData { NonSchemaProperties = data })
        };
    }

    private static SuggestedActions BuildSuggestedCommands(
        string? userId,
        params (string Title, string Value)[] items)
    {
        List<string> recipients = string.IsNullOrEmpty(userId) ? [] : [userId];
        List<Microsoft.Agents.Core.Models.CardAction> actions = items
            .Select(item => new Microsoft.Agents.Core.Models.CardAction
            {
                Type = "submit",
                Title = item.Title,
                Value = new { command = item.Value }
            })
            .ToList();
        return new SuggestedActions(recipients, actions);
    }

    private static async Task SendAsync(
        ITeamsTurnContext turnContext,
        IActivity activity,
        bool isTargeted,
        ChannelAccount? recipient,
        CancellationToken cancellationToken)
    {
        if (isTargeted)
        {
            activity.Recipient = CopyAccount(recipient);
            await turnContext.SendTargetedActivityAsync(activity, cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync(activity, cancellationToken);
        }
    }

    internal static IActivity CreateCardActivity(AdaptiveCard card, string? text = null)
    {
        IActivity activity = MessageFactory.Attachment(new Attachment
        {
            ContentType = ContentTypes.AdaptiveCard,
            Content = card
        });
        if (text is not null)
        {
            activity.Text = text;
        }
        return activity;
    }

    private static IActivity CreateTextActivity(string text) => MessageFactory.Text(text);

    private static ChannelAccount CopyAccount(ChannelAccount? account)
    {
        return new ChannelAccount(
            account?.Id ?? string.Empty,
            account?.Name,
            account?.AadObjectId,
            account?.AgenticUserId,
            account?.AgenticAppId,
            account?.TenantId,
            account?.Role);
    }

    private static string CreateReminderId()
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x");
        return $"{timestamp[^4..]}{Random.Shared.Next(0x10000):x4}";
    }

    private static string FormatTimeSpan(int milliseconds)
    {
        int totalSeconds = (int)Math.Round(milliseconds / 1000.0);
        if (totalSeconds >= 3600)
        {
            return $"{totalSeconds / 3600}h {(totalSeconds % 3600) / 60}m";
        }
        if (totalSeconds >= 60)
        {
            return $"{totalSeconds / 60}m {totalSeconds % 60}s";
        }
        return $"{totalSeconds}s";
    }

    private static string? GetJsonString(object? data, string propertyName)
    {
        if (data is JsonElement element &&
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement property))
        {
            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(data);
        return serialized.ValueKind == JsonValueKind.Object &&
            serialized.TryGetProperty(propertyName, out JsonElement value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;
    }

    private sealed class ParsedReminder
    {
        public string? TargetUserId { get; init; }
        public string? TargetUserName { get; init; }
        public string? ReminderText { get; init; }
        public int DelayMs { get; init; }
        public string? Error { get; init; }
    }
}
