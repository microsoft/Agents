# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from datetime import datetime

from semantic_kernel.functions import kernel_function


class DateTimePlugin:
    @kernel_function(name="date", description="Get the current date.")
    def date(self) -> str:
        return datetime.now().strftime("%A, %B %d, %Y")

    @kernel_function(name="today", description="Get the current date.")
    def today(self) -> str:
        return self.date()

    @kernel_function(
        name="now",
        description="Get the current date and time in the local time zone.",
    )
    def now(self) -> str:
        return datetime.now().strftime("%A, %B %d, %Y %I:%M %p")
