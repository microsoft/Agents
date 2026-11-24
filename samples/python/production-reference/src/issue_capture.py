# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

def advance_issue_capture(current: dict, message: str) -> tuple[dict, str]:
    if current.get("version") != 1:
        raise ValueError("Unsupported state schema.")
    value = message.strip()
    if not value:
        return current, "Please provide the requested information."
    if current["stage"] == "summary":
        return {
            **current,
            "summary": value,
            "stage": "impact",
        }, "What is the impact: low, medium, or high?"
    if current["stage"] == "impact":
        impact = value.lower()
        if impact not in {"low", "medium", "high"}:
            return current, "Reply with low, medium, or high."
        return {
            **current,
            "impact": impact,
            "stage": "complete",
        }, f"Support issue capture saved to this conversation with {impact} impact."
    if current["stage"] == "complete":
        return current, "This conversation already has a completed support issue capture."
    raise ValueError("Unsupported state stage.")
