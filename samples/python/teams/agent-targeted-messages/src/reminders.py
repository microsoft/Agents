# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
from dataclasses import dataclass, field
from typing import Any, Callable


@dataclass
class Reminder:
    identifier: str
    conversation_id: str
    target_id: str
    target_name: str
    creator_id: str
    creator_name: str
    text: str
    due_time: float
    cancel: Callable[[], None] | None = None
    service_url: str = ""
    channel_id: str = "msteams"
    conversation_type: str = ""


@dataclass
class AgentState:
    reminders: dict[str, Reminder] = field(default_factory=dict)
    cancellations: set[Callable[[], None]] = field(default_factory=set)
    tasks: set[asyncio.Task[None]] = field(default_factory=set)
    closed: bool = False
    sequence: int = 0


class ReminderScheduler:
    def __init__(
        self,
        state: AgentState,
        schedule: Callable[[Callable[[], Any], float], Callable[[], None]] | None = None,
    ) -> None:
        self.state = state
        self.schedule_override = schedule

    def schedule(
        self, callback: Callable[[], Any], delay: float
    ) -> Callable[[], None]:
        cancel_holder: dict[str, Callable[[], None]] = {}

        async def run_callback() -> None:
            try:
                await callback()
            finally:
                cancel = cancel_holder.get("cancel")
                if cancel is not None:
                    self.state.cancellations.discard(cancel)

        if self.schedule_override is not None:
            cancel = self.schedule_override(run_callback, delay)
            cancel_holder["cancel"] = cancel
            self.state.cancellations.add(cancel)
            return cancel

        async def run_scheduled_callback() -> None:
            await asyncio.sleep(delay)
            await run_callback()

        task = asyncio.create_task(run_scheduled_callback())
        cancel = task.cancel
        cancel_holder["cancel"] = cancel
        self.state.cancellations.add(cancel)
        self.state.tasks.add(task)

        def release_task(completed: asyncio.Task[None]) -> None:
            self.state.cancellations.discard(cancel)
            self.state.tasks.discard(completed)

        task.add_done_callback(release_task)
        return cancel

    def cancel(self, cancel: Callable[[], None] | None) -> None:
        if cancel is None:
            return
        cancel()
        self.state.cancellations.discard(cancel)

    async def close(self) -> None:
        if self.state.closed:
            return
        self.state.closed = True

        for cancel in tuple(self.state.cancellations):
            cancel()
            self.state.cancellations.discard(cancel)

        tasks = tuple(self.state.tasks)
        for task in tasks:
            task.cancel()
        if tasks:
            await asyncio.gather(*tasks, return_exceptions=True)

        self.state.tasks.clear()
        self.state.reminders.clear()
        self.state.cancellations.clear()
