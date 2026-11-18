# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import inspect
import os
import re
import time
from typing import Any, Callable

from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import AgentApplication, MemoryStorage, TurnState
from microsoft_agents.hosting.msteams import TeamsAgentExtension
from src.message_helpers import (
    _action,
    _activity,
    _card,
    _commands,
    _format_span,
    _read,
    _send_invoke_response,
    _targeted_payload,
)
from src.reminders import AgentState, Reminder, ReminderScheduler

load_dotenv()

TIME_PATTERN = re.compile(
    r"in\s+(\d+)\s*(seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h)\b",
    re.IGNORECASE,
)


def _seconds(text: str) -> int | None:
    match = TIME_PATTERN.search(text)
    if match is None:
        return None
    amount = int(match.group(1))
    unit = match.group(2).lower()
    if unit.startswith(("second", "sec")) or unit == "s":
        return amount
    if unit.startswith(("minute", "min")) or unit == "m":
        return amount * 60
    return amount * 3600


def create_agent(dependencies: dict[str, Any] | None = None) -> AgentApplication:
    dependencies = dependencies or {}
    configuration = load_configuration_from_env(os.environ)
    manager = dependencies.get("connection_manager")
    if manager is None:
        manager = MsalConnectionManager(**configuration)
    adapter = dependencies.get("adapter")
    if adapter is None:
        adapter = CloudAdapter(connection_manager=manager)
    application = AgentApplication[TurnState](
        storage=MemoryStorage(), adapter=adapter, connection_manager=manager
    )
    teams = TeamsAgentExtension(application)
    state = AgentState()
    scheduler = ReminderScheduler(state, dependencies.get("schedule"))
    now: Callable[[], float] = dependencies.get("now", time.time)
    reaction = dependencies.get("reaction")

    async def continue_delivery(reminder: Reminder) -> None:
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
            agent_id = (
                configuration.get("CONNECTIONS", {})
                .get("SERVICE_CONNECTION", {})
                .get("SETTINGS", {})
                .get("CLIENTID", "")
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
            async def deliver(turn_context: Any) -> None:
                await turn_context.send_activity(_activity(_targeted_payload(turn_context, card=card)))

            await adapter.continue_conversation(agent_id, continuation, deliver)
        except Exception as error:
            print(f"[REMINDER] Failed to deliver reminder {reminder.identifier}: {error}")
        finally:
            state.reminders.pop(reminder.identifier, None)

    async def set_reminder(context: Any, command: str, targeted: bool) -> None:
        activity = context.activity
        text = command.strip()
        text = re.sub(r"^remind\b", "", text, flags=re.IGNORECASE).strip()
        sender = _read(activity, "from_property")
        sender_id = _read(sender, "id", "")
        sender_name = _read(sender, "name", "You")
        target_id, target_name = sender_id, sender_name
        mentioned = None
        mentioned_text = ""
        for entity in _read(activity, "entities", []) or []:
            if _read(entity, "type") == "mention":
                person = _read(entity, "mentioned")
                if _read(person, "id") != _read(activity.recipient, "id"):
                    mentioned = person
                    mentioned_text = _read(entity, "text", "") or ""
                    break
        if not re.match(r"^me(?:\s|,|$)", text, re.IGNORECASE) and mentioned:
            target_id = _read(mentioned, "id", "")
            target_name = _read(mentioned, "name", "User")
        if mentioned_text:
            text = text.replace(mentioned_text, "").strip()
        text = re.sub(r"^me(?:\s|,|$)", "", text, flags=re.IGNORECASE).strip().lstrip(",").strip()
        delay = _seconds(text)
        if delay is None or delay > 2_147_483_647:
            await context.send_activity("Could not parse time. Use `remind me in 5 minutes`.\n\nUse `reminder-help` for usage examples.")
            return
        reminder_text = TIME_PATTERN.sub("", text).strip().lstrip(",").strip()
        reminder_text = re.sub(r"^(to|that)\s+", "", reminder_text, flags=re.IGNORECASE)
        reminder_text = reminder_text or "You have a reminder!"
        state.sequence += 1
        identifier = f"{int(now()) % 100000:05x}{state.sequence:04x}"
        conversation_id = _read(activity.conversation, "id", "").split(";")[0]
        reminder = Reminder(
            identifier, conversation_id, target_id, target_name, sender_id,
            sender_name, reminder_text, now() + delay,
            service_url=_read(activity, "service_url", "") or "",
            channel_id=_read(activity, "channel_id", "msteams") or "msteams",
            conversation_type=_read(_read(activity, "conversation"), "conversation_type", "") or "",
        )
        state.reminders[identifier] = reminder
        reminder.cancel = scheduler.schedule(
            lambda: continue_delivery(reminder), delay
        )
        body = [
            {"type": "TextBlock", "text": "Reminder Set!", "weight": "Bolder", "size": "Medium", "color": "Good"},
            {"type": "FactSet", "facts": [
                {"title": "Reminder:", "value": reminder_text},
                {"title": "For:", "value": "yourself" if sender_id == target_id else target_name},
                {"title": "In:", "value": _format_span(delay)},
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

    async def list_reminders(context: Any, targeted: bool) -> None:
        activity = context.activity
        user_id = _read(_read(activity, "from_property"), "id")
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
        lines = [
            f'- **{item.identifier}**: "{item.text}" for '
            f'{"yourself" if item.creator_id == item.target_id else item.target_name} '
            f'({"in " + _format_span(item.due_time - now()) if item.due_time > now() else "overdue"})'
            for item in own
        ]
        await context.send_activity(
            _activity(_targeted_payload(
                context,
                text="**Your Active Reminders:**\n\n" + "\n".join(lines)
                + "\n\nUse `cancel-reminder [id]` to cancel a reminder.",
            ))
        )

    async def cancel_reminder(context: Any, identifier: str) -> None:
        user_id = _read(_read(context.activity, "from_property"), "id")
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

    async def show_help(context: Any) -> None:
        user_id = _read(_read(context.activity, "from_property"), "id")
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
        context: Any,
        command: str,
        targeted: bool,
        quoted_id: str | None = None,
        from_invoke: bool = False,
    ) -> None:
        lower = command.lower().strip()
        if lower in ("reminder-help", "help"):
            await show_help(context)
        elif lower.startswith("remind"):
            await set_reminder(context, command, targeted)
        elif lower == "my-reminders":
            await list_reminders(context, targeted)
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
            response["suggestedActions"] = _commands(_read(_read(context.activity, "from_property"), "id"), [
                ("Show help", "reminder-help"),
                ("Remind me in 5 minutes test", "remind me in 5 minutes test"),
                ("My reminders", "my-reminders"),
            ])
            response["entities"].append(
                {"type": "https://schema.org/Message", "additionalType": ["AIGeneratedContent"]}
            )
            await context.send_activity(_activity(response))

    async def handle_reaction(
        context: Any,
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
        message_id = quoted_id or _read(activity, "reply_to_id") or _read(activity, "id")
        if reaction is not None:
            await reaction(context, operation, message_id, reaction_type)
        else:
            client = teams.get_teams_api_client(context)
            if inspect.isawaitable(client):
                client = await client
            method = client.reactions.add if operation == "add" else client.reactions.delete
            await method(_read(activity.conversation, "id"), message_id, reaction_type)
        verb = "Added" if operation == "add" else "Removed"
        await context.send_activity(f"{verb} a **{reaction_type}** reaction to your message!")

    @application.activity("message")
    async def on_message(context: Any, _state: TurnState) -> None:
        activity = context.activity
        raw_text = _read(activity, "text", "") or ""
        if not raw_text:
            return
        text = raw_text
        bot_id = _read(activity.recipient, "id", "")
        for entity in _read(activity, "entities", []) or []:
            if _read(entity, "type") == "mention" and _read(_read(entity, "mentioned"), "id") == bot_id:
                mention_text = _read(entity, "text", "")
                text = text.replace(mention_text, "")
        quoted_id = None
        quoted = re.search(r'<quoted\s+messageId="([^"]+)"\s*/>', text)
        if quoted:
            quoted_id = quoted.group(1)
            text = text.replace(quoted.group(0), "")
        targeted = bool(_read(activity.recipient, "is_targeted", False))
        await route_command(context, text.strip(), targeted, quoted_id)

    async def cancel_card_reminder(context: Any, data: Any) -> str:
        activity = context.activity
        identifier = _read(data, "reminderId")
        reminder = state.reminders.get(identifier) if identifier else None
        user_id = _read(_read(activity, "from_property"), "id")
        if reminder is None:
            return "Reminder not found or already completed."
        if user_id not in (reminder.creator_id, reminder.target_id):
            return "You can only cancel reminders you created or are assigned to you."
        scheduler.cancel(reminder.cancel)
        state.reminders.pop(identifier, None)
        return "Reminder cancelled!"

    async def dismiss_card_reminder(context: Any, data: Any) -> str:
        activity = context.activity
        identifier = _read(data, "reminderId")
        dismissed = state.reminders.get(identifier) if identifier else None
        user_id = _read(_read(activity, "from_property"), "id")
        if dismissed is None:
            return "Reminder dismissed!"
        if user_id not in (dismissed.creator_id, dismissed.target_id):
            return "You can only dismiss reminders you created or are assigned to you."
        scheduler.cancel(dismissed.cancel)
        state.reminders.pop(identifier, None)
        return "Reminder dismissed!"

    async def snooze_card_reminder(context: Any, data: Any) -> str:
        activity = context.activity
        text = _read(data, "reminderText", "Snoozed reminder") or "Snoozed reminder"
        raw_minutes = _read(data, "snoozeMinutes", "5")
        if raw_minutes is None:
            raw_minutes = "5"
        try:
            if isinstance(raw_minutes, bool) or not isinstance(raw_minutes, (str, int)):
                raise ValueError("Snooze duration must be an integer.")
            if isinstance(raw_minutes, str) and not re.fullmatch(r"\d+", raw_minutes.strip()):
                raise ValueError("Snooze duration must be a nonnegative integer.")
            minutes = int(raw_minutes)
            if minutes < 0 or minutes * 60 > 2_147_483_647:
                raise ValueError("Snooze duration is too large.")
        except ValueError:
            return "Invalid snooze duration."
        activity_conversation = _read(activity, "conversation")
        sender = _read(activity, "from_property")
        state.sequence += 1
        new_reminder = Reminder(
            f"{int(now()) % 100000:05x}{state.sequence:04x}",
            _read(activity_conversation, "id", "").split(";")[0],
            _read(sender, "id", ""),
            _read(sender, "name", "User"),
            _read(sender, "id", ""),
            _read(sender, "name", "User"),
            text,
            now() + minutes * 60,
            service_url=_read(activity, "service_url", "") or "",
            channel_id=_read(activity, "channel_id", "msteams") or "msteams",
            conversation_type=_read(activity_conversation, "conversation_type", "") or "",
        )
        state.reminders[new_reminder.identifier] = new_reminder
        new_reminder.cancel = scheduler.schedule(
            lambda: continue_delivery(new_reminder), minutes * 60
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
    async def on_invoke(context: Any, _state: TurnState) -> None:
        activity = context.activity
        name = _read(activity, "name", "")
        value = _read(activity, "value", {})
        if name == "suggestedActions/submit":
            command = _read(value, "command")
            if command:
                await route_command(context, command, False, from_invoke=True)
            else:
                await context.send_activity("No command specified.")
            await _send_invoke_response(context)
        elif name == "adaptiveCard/action":
            data = _read(_read(value, "action"), "data", {})
            action = _read(data, "action")
            handler = card_actions.get(action)
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
    async def on_reaction_event(context: Any, _state: TurnState) -> None:
        activity = context.activity
        for item in _read(activity, "reactions_added", []) or []:
            user = _read(item, "user")
            name = _read(user, "display_name", None) or "Someone"
            kind = _read(item, "type", "unknown")
            await context.send_activity(f"Thanks for the **{kind}** reaction, {name}!")
        for item in _read(activity, "reactions_removed", []) or []:
            user = _read(item, "user")
            name = _read(user, "display_name", None) or "Someone"
            kind = _read(item, "type", "unknown")
            await context.send_activity(f"Noted! {name} removed their **{kind}** reaction.")

    async def close_background_tasks() -> None:
        await scheduler.close()

    application.close_background_tasks = close_background_tasks
    application.reminders = state.reminders
    application.teams = teams
    return application
