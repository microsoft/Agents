# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from microsoft_agents.activity import ActivityTypes
from microsoft_agents.activity.config import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    AgentApplication,
    Authorization,
    OutboundHostValidator,
)
from microsoft_agents.hosting.core.authorization import AuthTypes

from .issue_capture import advance_issue_capture


def create_agent(config, storage, telemetry):
    sdk_config = load_configuration_from_env(
        {
            "Connections": {
                "SERVICE_CONNECTION": {
                    "Settings": {
                        "ClientId": config.client_id or None,
                        "TenantId": config.tenant_id or None,
                        "AuthType": (
                            AuthTypes.user_managed_identity
                            if config.production
                            else AuthTypes.client_secret
                        ),
                        "ClientSecret": config.client_secret or None,
                        "Anonymous_Allowed": not config.production,
                        "Validate_Issuer": config.production,
                        "Issuers": ["https://api.botframework.com"],
                    }
                }
            },
            "ConnectionsMap": [
                {
                    "ServiceUrl": "*",
                    "Connection": "SERVICE_CONNECTION",
                    "Audience": config.client_id,
                }
            ],
            "TokenValidation": {
                "Audiences": [config.client_id] if config.client_id else [],
                "Anonymous_Allowed": not config.production,
            },
        }
    )
    connections = MsalConnectionManager(config=sdk_config)
    adapter = CloudAdapter(
        connection_manager=connections,
        host_validator=OutboundHostValidator(
            enabled=config.production,
            hosts=["webchat.botframework.com"],
            include_default_microsoft_hosts=False,
        ),
    )
    agent = AgentApplication(
        storage=storage,
        authorization=Authorization(storage, connections, config=sdk_config),
        start_typing_timer=False,
        remove_recipient_mention=False,
    )

    @agent.after_turn
    async def save_state(context, state):
        return True

    @agent.conversation_update("membersAdded")
    async def welcome(context, state):
        members = context.activity.members_added or []
        if any(member.id != context.activity.recipient.id for member in members):
            await context.send_activity(
                "Welcome. Describe the support issue you want to report, "
                'for example: "Checkout is unavailable."'
            )

    @agent.activity(ActivityTypes.message)
    async def capture(context, state):
        with telemetry.tracer.start_as_current_span(
            "agent.support_issue_capture.turn",
            record_exception=False,
            set_status_on_exception=False,
        ) as span:
            try:
                current = state.get_value("ConversationState.supportIssueCapture") or {
                    "version": 1,
                    "stage": "summary",
                }
                updated, reply = advance_issue_capture(current, context.activity.text or "")
                state.set_value("ConversationState.supportIssueCapture", updated)
                # Persist before acknowledging success. Conflicts fail safely;
                # replaying a whole turn could duplicate channel replies.
                await state.save(context)
                telemetry.turns.add(1)
                await context.send_activity(reply)
            except Exception:
                telemetry.failed_span(span)
                raise

    @agent.error
    async def safe_error(context, error):
        telemetry.failure("turn")
        await context.send_activity(
            "Sorry, the request could not be processed. Please try again later."
        )

    return agent, adapter, sdk_config
