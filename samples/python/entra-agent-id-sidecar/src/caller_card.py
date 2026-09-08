# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from datetime import datetime, timezone
from typing import Any

from microsoft_agents.hosting.core import CardFactory, TurnContext


def create_caller_card(context: TurnContext):
    identity = context.identity
    claims = identity.claims if identity else {}
    recipient = context.activity.recipient

    facts = [
        _fact("Claims available", "Yes" if claims else "No"),
        _fact("Authenticated", "Yes" if identity and identity.is_authenticated else "No"),
        _fact("Authentication type", identity.authentication_type if identity else None),
        _fact("Issuer", _claim(claims, "iss")),
        _fact("Audience", _claim(claims, "aud")),
        _fact(
            "Tenant ID",
            _claim(claims, "tid", "http://schemas.microsoft.com/identity/claims/tenantid"),
        ),
        _fact(
            "Subject",
            _claim(
                claims,
                "sub",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
            ),
        ),
        _fact("Caller app ID", _claim(claims, "azp", "appid")),
        _fact("Token version", _claim(claims, "ver")),
        _fact("Issued", _epoch_claim(claims, "iat")),
        _fact("Expires", _epoch_claim(claims, "exp")),
        _fact("Recipient role", recipient.role if recipient else None),
        _fact("Agent identity", recipient.agentic_app_id if recipient else None),
        _fact("Agent user", recipient.agentic_user_id if recipient else None),
        _fact("Activity tenant", recipient.tenant_id if recipient else None),
    ]

    return CardFactory.adaptive_card(
        {
            "type": "AdaptiveCard",
            "version": "1.5",
            "body": [
                {
                    "type": "TextBlock",
                    "size": "Medium",
                    "weight": "Bolder",
                    "text": "Inbound agentic caller",
                },
                {
                    "type": "TextBlock",
                    "wrap": True,
                    "text": (
                        "Selected token claims and activity routing values. "
                        "The raw token is never displayed."
                    ),
                },
                {"type": "FactSet", "facts": facts},
            ],
        }
    )


def _fact(title: str, value: Any) -> dict[str, str]:
    if isinstance(value, list):
        value = ", ".join(str(item) for item in value)
    text = str(value).strip() if value is not None else ""
    return {"title": title, "value": text or "Unavailable"}


def _claim(claims: dict[str, Any], *names: str):
    return next((claims[name] for name in names if name in claims), None)


def _epoch_claim(claims: dict[str, Any], name: str):
    value = _claim(claims, name)
    try:
        return datetime.fromtimestamp(int(value), timezone.utc).isoformat()
    except (TypeError, ValueError, OverflowError):
        return value
