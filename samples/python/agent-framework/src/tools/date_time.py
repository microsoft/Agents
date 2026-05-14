# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from datetime import datetime
from typing import Annotated

from agent_framework import tool
from pydantic import Field


@tool(approval_mode="never_require")
def get_date(
    input: Annotated[str, Field(description="Any input, not used")] = "",
) -> str:
    """Use this tool to get the current date and time."""
    return datetime.now().strftime("%A, %B %d, %Y %I:%M %p")
