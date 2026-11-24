# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import re
import time
from collections.abc import Awaitable, Callable

from microsoft_agents.activity import Activity, ChannelAccount
from microsoft_agents.hosting.core import (
    AgentApplication,
    ChannelServiceAdapter,
    MemoryStorage,
    TurnContext,
    TurnState,
)
from microsoft_agents.hosting.core.authorization import Connections
from microsoft_agents.hosting.msteams import TeamsAgentExtension
from src.message_helpers import (
    _action,
    _activity,
    _card,
    _commands,
    _format_span,
    is_targeted,
    _send_invoke_response,
    _targeted_payload,
)
from src.reminders import AgentState, Reminder, ReminderScheduler, parse_reminder

MAX_DELAY_SECONDS = 2_147_483_647


def _sender_id(activity: Activity) -> str | None:
    return activity.from_property.id if activity.from_property else None


def _reminder_from_activity(
    activity: Activity,
    *,
    identifier: str,
    target: ChannelAccount | None,
    text: str,
    due_time: float,
) -> Reminder:
    """Capture the conversation and accounts needed after the incoming turn ends."""
    sender = activity.from_property or ChannelAccount()
    conversation = activity.conversation
    recipient = target if target is not None else sender
    default_recipient_name = "User" if target is not None else "You"
    return Reminder(
        identifier=identifier,
        conversation_id=(conversation.id or "").split(";")[0] if conversation else "",
        target_id=recipient.id or "",
        target_name=recipient.name or default_recipient_name,
        creator_id=sender.id or "",
        creator_name=sender.name or "You",
        text=text,
        due_time=due_time,
        service_url=activity.service_url or "",
        channel_id=activity.channel_id or "msteams",
        conversation_type=(conversation.conversation_type or "") if conversation else "",
    )


def create_agent(
    configuration: dict,
    connection_manager: Connections,
    *,
    agent_id: str = "",
    schedule: Callable[[Callable[[], Awaitable[None]], float], Callable[[], None]] | None = None,
    now: Callable[[], float] = time.time,
    reaction: Callable[[TurnContext, str, str | None, str], Awaitable[None]] | None = None,
) -> AgentApplication:
    application = AgentApplication[TurnState](
        storage=MemoryStorage(), connection_manager=connection_manager, **configuration
    )
    teams = TeamsAgentExtension(application)
    state = AgentState()
    scheduler = ReminderScheduler(state, schedule)

    async def continue_delivery(reminder: Reminder, adapter: ChannelServiceAdapter) -> None:
        if reminder.identifier not in state.reminders or state.closed:
            return
        try:
            card = _card(
                [
                    {"type": "TextBlock", "text": "Reminder", "weight": "Bolder", "size": "Large", "color": "Accent"},
                    {"type": "TextBlock", "text": reminder.text, "wrap": True, "size": "Medium"},
                    {"type": "TextBlock", "text": f"Set by {reminder.creator_name}", "size": "Small", "isSubtle": True},
                ],
                [
                    _action("Dismiss", "dismiss_reminder", {"action": "dismiss_reminder", "reminderId": reminder.identifier}),
                    _action(
                        "Snooze 5 min",
                        "snooze_reminder",
                        {
                            "action": "snooze_reminder",
                            "reminderId": reminder.identifier,
                            "reminderText": reminder.text,
                            "snoozeMinutes": "5",
                        },
                    ),
                ],
            )
            # The continuation's sender becomes the outgoing recipient, so the
            # reminder is addressed to its captured target user.
            continuation = _activity(
                {
                    "type": "message",
                    "channelId": reminder.channel_id,
                    "serviceUrl": reminder.service_url,
                    "conversation": {"id": reminder.conversation_id, "conversationType": reminder.conversation_type},
                    "from": {"id": reminder.target_id, "name": reminder.target_name},
                    "recipient": {"id": agent_id or "agent"},
                }
            )
            async def deliver(turn_context: TurnContext) -> None:
                await turn_context.send_activity(_activity(_targeted_payload(turn_context, card=card)))

            await adapter.continue_conversation(agent_id, continuation, deliver)
        except Exception as error:
            print(f"[REMINDER] Failed to deliver reminder {reminder.identifier}: {error}")
        finally:
            state.reminders.pop(reminder.identifier, None)

    def schedule_reminder(
        context: TurnContext,
        text: str,
        delay: int,
        target: ChannelAccount | None = None,
    ) -> Reminder:
        current_time = now()
        state.sequence += 1
        identifier = f"{int(current_time) % 100000:05x}{state.sequence:04x}"
        reminder = _reminder_from_activity(
            context.activity,
            identifier=identifier,
            target=target,
            text=text,
            due_time=current_time + delay,
        )
        adapter = context.adapter
        state.reminders[identifier] = reminder
        reminder.cancel = scheduler.schedule(lambda: continue_delivery(reminder, adapter), delay)
        return reminder

    async def set_reminder(context: TurnContext, command: str) -> None:
        activity = context.activity
        bot_id = activity.recipient.id if activity.recipient else None
        mention = next(
            (
                mention for mention in activity.get_mentions()
                if mention.mentioned and mention.mentioned.id != bot_id
            ),
            None,
        )
        parsed = parse_reminder(command, mention.text if mention else None)
        if parsed is None or parsed.delay > MAX_DELAY_SECONDS:
            await context.send_activity(
                "Could not parse time. Use `remind me in 5 minutes`.\n\n"
                "Use `reminder-help` for usage examples."
            )
            return

        target = mention.mentioned if mention and not parsed.for_self else None
        reminder = schedule_reminder(context, parsed.text, parsed.delay, target)
        identifier = reminder.identifier
        recipient_name = (
            "yourself" if reminder.creator_id == reminder.target_id else reminder.target_name
        )
        body = [
            {"type": "TextBlock", "text": "Reminder Set!", "weight": "Bolder", "size": "Medium", "color": "Good"},
            {"type": "FactSet", "facts": [
                {"title": "Reminder:", "value": parsed.text},
                {"title": "For:", "value": recipient_name},
                {"title": "In:", "value": _format_span(parsed.delay)},
                {"title": "ID:", "value": identifier},
            ]},
            {"type": "TextBlock", "text": "This is a targeted message - only you can see this.", "size": "Small", "isSubtle": True, "wrap": True},
        ]
        card = _card(body, [_action("Cancel Reminder", "cancel_reminder", {"action": "cancel_reminder", "reminderId": identifier})])
        payload = _targeted_payload(context, card=card)
        confirmed = False
        try:
            await context.send_activity(_activity(payload))
            confirmed = True
        finally:
            if not confirmed:
                state.reminders.pop(identifier, None)
                scheduler.cancel(reminder.cancel)

    async def list_reminders(context: TurnContext) -> None:
        user_id = _sender_id(context.activity)
        own = sorted(
            (item for item in state.reminders.values() if item.target_id == user_id or item.creator_id == user_id),
            key=lambda item: item.due_time,
        )
        if not own:
            response = _targeted_payload(context, text="You have no active reminders.")
            response["suggestedActions"] = _commands(user_id, [
                ("Remind me in 5 minutes test", "remind me in 5 minutes test"),
                ("Remind me in 1 hour meeting", "remind me in 1 hour meeting"),
                ("Show help", "reminder-help"),
            ])
            await context.send_activity(_activity(response))
            return
        current_time = now()
        lines = []
        for reminder in own:
            recipient_name = (
                "yourself" if reminder.creator_id == reminder.target_id else reminder.target_name
            )
            remaining = reminder.due_time - current_time
            due = f"in {_format_span(remaining)}" if remaining > 0 else "overdue"
            lines.append(f'- **{reminder.identifier}**: "{reminder.text}" for {recipient_name} ({due})')
        await context.send_activity(
            _activity(_targeted_payload(
                context,
                text="**Your Active Reminders:**\n\n" + "\n".join(lines)
                + "\n\nUse `cancel-reminder [id]` to cancel a reminder.",
            ))
        )

    async def cancel_reminder(context: TurnContext, identifier: str) -> None:
        user_id = _sender_id(context.activity)
        reminder = state.reminders.get(identifier)
        if reminder is None:
            message = f"Reminder **{identifier}** not found or already completed." if identifier else "Please specify a reminder ID. Use `my-reminders` to see your active reminders."
        elif user_id not in (reminder.creator_id, reminder.target_id):
            message = "You can only cancel reminders you created or are assigned to you."
        else:
            scheduler.cancel(reminder.cancel)
            state.reminders.pop(identifier, None)
            message = f"Reminder **{identifier}** has been cancelled."
        await context.send_activity(_activity(_targeted_payload(context, text=message)))

    async def show_help(context: TurnContext) -> None:
        user_id = _sender_id(context.activity)
        text = (
            "**Personal Reminder Agent - Help**\n\n"
            "**Set a Reminder:**\n"
            "- `remind me in 5 minutes to check email`\n"
            "- `remind me in 1 hour meeting starts`\n"
            "- `remind me in 30 seconds test`\n"
            "- `remind @John in 10 minutes review PR`\n\n"
            "**Manage Reminders:**\n"
            "- `my-reminders` - View your active reminders\n"
            "- `cancel-reminder [id]` - Cancel a specific reminder\n"
            "- `reminder-help` - Show this help message\n\n"
            "**Reactions:**\n"
            "- `add-reaction [type]` - Add a reaction to a message\n"
            "- `remove-reaction [type]` - Remove a reaction from a message\n"
            "Reactions cannot be used from slash-command messages."
        )
        response = _targeted_payload(context, text=text)
        response["suggestedActions"] = _commands(user_id, [
            ("Set a 30-second test reminder", "remind me in 30 seconds test"),
            ("Set a 5-minute reminder", "remind me in 5 minutes check email"),
            ("My reminders", "my-reminders"),
        ])
        await context.send_activity(_activity(response))

    async def route_command(
        context: TurnContext,
        command: str,
        targeted: bool,
        quoted_id: str | None = None,
        from_invoke: bool = False,
    ) -> None:
        lower = command.lower().strip()
        if lower in ("reminder-help", "help"):
            await show_help(context)
        elif lower.startswith("remind"):
            await set_reminder(context, command)
        elif lower == "my-reminders":
            await list_reminders(context)
        elif from_invoke:
            # Suggested-action invokes only share the first three commands.
            await context.send_activity(f"Executing: {command}")
        elif lower.startswith("cancel-reminder"):
            await cancel_reminder(context, re.sub(r"^cancel-reminder\s*", "", command, flags=re.IGNORECASE))
        elif lower.startswith("add-reaction"):
            await handle_reaction(context, command, targeted, "add", quoted_id)
        elif lower.startswith("remove-reaction"):
            await handle_reaction(context, command, targeted, "delete", quoted_id)
        else:
            response = _targeted_payload(context, text="Use `reminder-help` to see available commands.")
            response["suggestedActions"] = _commands(_sender_id(context.activity), [
                ("Show help", "reminder-help"),
                ("Remind me in 5 minutes test", "remind me in 5 minutes test"),
                ("My reminders", "my-reminders"),
            ])
            response["entities"].append(
                {"type": "https://schema.org/Message", "additionalType": ["AIGeneratedContent"]}
            )
            await context.send_activity(_activity(response))

    async def handle_reaction(
        context: TurnContext,
        command: str,
        targeted: bool,
        operation: str,
        quoted_id: str | None = None,
    ) -> None:
        activity = context.activity
        name = "add-reaction" if operation == "add" else "remove-reaction"
        reaction_type = re.sub(rf"^{name}\s*", "", command, flags=re.IGNORECASE).strip()
        if not reaction_type:
            await context.send_activity(f"Please specify a reaction type. Example: `{name} like`")
            return
        if targeted:
            await context.send_activity(
                _activity(_targeted_payload(
                    context,
                    text=f"Reactions cannot be {'added' if operation == 'add' else 'removed'} via slash commands. Please use `{name}` by @mentioning the agent in a regular message.",
                ))
            )
            return
        # Quoted message, then reply target, then the incoming message.
        message_id = quoted_id or activity.reply_to_id or activity.id
        if reaction is not None:
            await reaction(context, operation, message_id, reaction_type)
        else:
            client = teams.get_teams_api_client(context)
            method = client.reactions.add if operation == "add" else client.reactions.delete
            conversation_id = activity.conversation.id if activity.conversation else None
            await method(conversation_id, message_id, reaction_type)
        verb = "Added" if operation == "add" else "Removed"
        await context.send_activity(f"{verb} a **{reaction_type}** reaction to your message!")

    @application.activity("message")
    async def on_message(context: TurnContext, _state: TurnState) -> None:
        activity = context.activity
        text = activity.text or ""
        if not text:
            return

        bot_id = activity.recipient.id if activity.recipient else None
        for mention in activity.get_mentions():
            if mention.mentioned and mention.mentioned.id == bot_id and mention.text:
                text = text.replace(mention.text, "")

        quoted_id = None
        quoted = re.search(r'<quoted\s+messageId="([^"]+)"\s*/>', text)
        if quoted:
            quoted_id = quoted.group(1)
            text = text.replace(quoted.group(0), "")
        await route_command(context, text.strip(), is_targeted(activity), quoted_id)

    async def cancel_card_reminder(context: TurnContext, data: dict) -> str:
        identifier = data.get("reminderId")
        reminder = state.reminders.get(identifier) if isinstance(identifier, str) else None
        user_id = _sender_id(context.activity)
        if reminder is None:
            return "Reminder not found or already completed."
        if user_id not in (reminder.creator_id, reminder.target_id):
            return "You can only cancel reminders you created or are assigned to you."
        scheduler.cancel(reminder.cancel)
        state.reminders.pop(identifier, None)
        return "Reminder cancelled!"

    async def dismiss_card_reminder(context: TurnContext, data: dict) -> str:
        identifier = data.get("reminderId")
        dismissed = state.reminders.get(identifier) if isinstance(identifier, str) else None
        user_id = _sender_id(context.activity)
        if dismissed is None:
            return "Reminder dismissed!"
        if user_id not in (dismissed.creator_id, dismissed.target_id):
            return "You can only dismiss reminders you created or are assigned to you."
        scheduler.cancel(dismissed.cancel)
        state.reminders.pop(identifier, None)
        return "Reminder dismissed!"

    async def snooze_card_reminder(context: TurnContext, data: dict) -> str:
        text = data.get("reminderText") or "Snoozed reminder"
        raw_minutes = data.get("snoozeMinutes")
        if raw_minutes is None:
            raw_minutes = 5
        if isinstance(raw_minutes, str) and re.fullmatch(r"\d+", raw_minutes.strip()):
            try:
                raw_minutes = int(raw_minutes)
            except ValueError:
                return "Invalid snooze duration."
        if (
            type(raw_minutes) is not int
            or raw_minutes < 0
            or raw_minutes * 60 > MAX_DELAY_SECONDS
        ):
            return "Invalid snooze duration."

        minutes = raw_minutes
        new_reminder = schedule_reminder(
            context, text, minutes * 60, target=context.activity.from_property
        )
        card = _card(
            [{"type": "TextBlock", "text": text, "wrap": True}],
            [_action("Cancel", "cancel_reminder", {"action": "cancel_reminder", "reminderId": new_reminder.identifier})],
        )
        await context.send_activity(_activity(_targeted_payload(context, text=f"Snoozed for {minutes} minutes", card=card)))
        return f"Snoozed for {minutes} minutes!"

    card_actions = {
        "cancel_reminder": cancel_card_reminder,
        "dismiss_reminder": dismiss_card_reminder,
        "snooze_reminder": snooze_card_reminder,
    }

    @application.activity("invoke")
    async def on_invoke(context: TurnContext, _state: TurnState) -> None:
        activity = context.activity
        name = activity.name
        value = activity.value if isinstance(activity.value, dict) else {}
        if name == "suggestedActions/submit":
            command = value.get("command")
            if isinstance(command, str) and command:
                await route_command(context, command, False, from_invoke=True)
            else:
                await context.send_activity("No command specified.")
            await _send_invoke_response(context)
        elif name == "adaptiveCard/action":
            action = value.get("action")
            data = action.get("data") if isinstance(action, dict) else None
            data = data if isinstance(data, dict) else {}
            action_name = data.get("action")
            handler = card_actions.get(action_name) if isinstance(action_name, str) else None
            result = await handler(context, data) if handler else "Unknown action."
            await _send_invoke_response(
                context,
                {
                    "statusCode": 200,
                    "type": "application/vnd.microsoft.activity.message",
                    "value": result,
                },
            )

    @application.activity("messageReaction")
    async def on_reaction_event(context: TurnContext, _state: TurnState) -> None:
        activity = context.activity
        for reaction in activity.reactions_added or []:
            await context.send_activity(f"Thanks for the **{reaction.type}** reaction, Someone!")
        for reaction in activity.reactions_removed or []:
            await context.send_activity(f"Noted! Someone removed their **{reaction.type}** reaction.")

    async def close_background_tasks() -> None:
        await scheduler.close()

    application.close_background_tasks = close_background_tasks
    application.reminders = state.reminders
    application.teams = teams
    return application
