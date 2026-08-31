# Proactive

This sample shows how an agent sends a message when no user activity starts the turn. A scheduler, webhook, or background job can use this pattern to notify an existing conversation.

## Patterns demonstrated

| Pattern | Command or endpoint |
|---|---|
| Store a conversation reference | `-s` |
| Continue the current conversation | `-c` |
| Continue a stored conversation | `-c <conversation-id>` |
| Export conversation data | `-convo` |
| Echo through a custom continuation activity | Any other message |
| Send an activity to a stored conversation | `POST /proactive/sendActivity/{conversationId}` |
| Continue from supplied conversation data | `POST /proactive/continue` |

## Prerequisites

- [Node.js 20 or later](https://nodejs.org/)
- An Azure subscription
- An [Azure Bot](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md) with a client ID, client secret, and tenant ID
- [Dev tunnels](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started) for local channel testing
- A tool that can send HTTP requests, such as `curl`

## Azure Bot configuration

1. Create an Azure Bot with single-tenant client-secret authentication.
2. Start an anonymous tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

3. In the Azure Bot configuration, set the messaging endpoint to:

   ```text
   https://<tunnel-host>/api/messages
   ```

The `appManifest` directory contains a Microsoft Teams app manifest. Replace `AAD_APP_CLIENT_ID` and `BOT_DOMAIN` through your provisioning process before you package and upload it to Teams.

## Local configuration

Copy `env.TEMPLATE` to `.env`. Set:

- `connections__serviceConnection__settings__clientId`
- `connections__serviceConnection__settings__clientSecret`
- `connections__serviceConnection__settings__tenantId`

Keep `NODE_ENV=development` for local testing. `ALLOWED_CALLERS` is required outside Development. Set it to a comma-separated list of application IDs permitted to call proactive HTTP routes.

Do not commit `.env` or store production secrets in it.

## Install and run

From `samples/nodejs/proactive`:

```bash
npm install
npm start
```

The agent listens on `http://localhost:3978`.

## Test in Web Chat

Open **Test in Web Chat** in the Azure Bot resource. Use these commands:

1. Send `-s`. Save the returned conversation ID.
2. Send `-c`. The agent sends `This is OnContinueConversation` from a proactive turn.
3. Send `-c <conversation-id>`. The stored conversation gets the same message.
4. Send `-convo`. Save the returned JSON for the continue endpoint.
5. Send any other text. The agent echoes it from a proactive turn.

## Test proactive HTTP routes

Development mode does not require authentication for these routes.

### Send an activity to a stored conversation

First send `-s`. Then run:

```bash
curl -X POST "http://localhost:3978/proactive/sendActivity/<conversation-id>" \
  -H "Content-Type: application/json" \
  -d '{"type":"message","text":"Your job finished successfully."}'
```

The stored conversation receives the supplied activity.

### Continue from conversation data

Send `-convo`. Save the returned JSON as `conversation.json`. Then run:

```bash
curl -X POST "http://localhost:3978/proactive/continue" \
  -H "Content-Type: application/json" \
  --data-binary @conversation.json
```

The conversation receives `This is OnContinueConversation`.

### HTTP responses

| Status | Meaning |
|---|---|
| `200` | The activity was sent or the conversation was continued. |
| `400` | The JSON or conversation data is invalid. |
| `401` | The JWT is missing or invalid outside Development. |
| `403` | The caller application ID is not allowed. |
| `404` | The stored conversation does not exist. |
| `500` | The adapter or channel operation failed. |

## Authentication and caller authorization

The proactive routes require JWT authentication outside Development. `ALLOWED_CALLERS` limits callers by application ID. The process stops at startup when this setting is empty outside Development.

Do not use Development in a deployed service.

## Production storage

This sample uses `MemoryStorage`. Stored conversation references are lost after restart and are not shared across service instances. Use durable shared storage in production.

Conversation references contain service and participant identifiers. Apply your data retention and access-control requirements.

## Troubleshooting

- **Conversation not found:** Send `-s` again. Memory storage was empty or the process restarted.
- **401 response:** Verify the bearer token, Azure Bot client ID, tenant ID, and `NODE_ENV`.
- **403 response:** Add the caller application ID to `ALLOWED_CALLERS`.
- **No proactive message:** Verify the conversation is still valid and the channel service URL is reachable.

## Further reading

- [Proactive messages](https://learn.microsoft.com/azure/bot-service/bot-builder-howto-proactive-message)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/Agents)
