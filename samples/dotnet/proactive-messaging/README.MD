# Microsoft Copilot Studio Teams Proactive Messaging Sample

This sample shows how to (1) create a new conversation or (2) send a proactive message into an existing user–agent conversation in Microsoft Teams using the Microsoft 365 Agents SDK.

> **Important:** Proactive messaging is only supported on the Microsoft Teams channel.

## Proactive Messaging Scenarios
A proactive message is any message a Copilot Studio agent sends that is not directly triggered by the user’s current input. Common scenarios:

- **Notify in an existing conversation:** Push alerts or status updates into an active user–agent Teams chat or channel thread.
- **Relay external events:** Forward events or external system notifications back into the ongoing conversation with the user.
- **Start a new conversation:** Initiate a fresh 1:1 (or channel) conversation when a monitored condition or business rule is met.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)
- Copilot Studio Agent published to the Microsoft Teams channel ([publish guide](https://learn.microsoft.com/en-us/microsoft-copilot-studio/publication-add-bot-to-microsoft-teams))

## Run the Sample Locally

1. Restore and build the project:
   ```bash
   cd samples/dotnet/proactive-messaging
   dotnet build
   ```
2. Run the sample (Kestrel is bound to `http://localhost:5199` when `ASPNETCORE_ENVIRONMENT=Development`):
   ```bash
   dotnet run
   ```
3. Create a new conversation:
   > **Note:** `UserAadObjectId` is the user's Object ID from Microsoft Entra ID (Azure AD).s 

   ```bash
   curl -X POST http://localhost:5199/api/createconversation \
     -H 'Content-Type: application/json' \
     -d '{
       "Message": "Hello from proactive sample!",
       "UserAadObjectId": "00000000-0000-0000-0000-000000000123"
     }'
   ```
   Example response:
   ```json
   { "conversationId": "<conversation-id>" }
   ```
4. Send another proactive message to that conversation:
   > **Note:** `ConversationId` is the value returned by the create conversation step above.

   ```bash
   curl -X POST http://localhost:5199/api/sendmessage \
     -H 'Content-Type: application/json' \
     -d '{
       "ConversationId": "<conversation-id>",
       "Message": "Proactive Message"
     }'
   ```

## Enabling JWT Token Validation

By default, JWT token validation is disabled to simplify local debugging. To enable it, update `appsettings.json`:

```json
"TokenValidation": {
  "Enabled": true,
  "Audiences": [
    "{{ClientId}}" // Client ID (Application ID) of the Azure AD app / Bot registration
  ],
  "TenantId": "{{TenantId}}"
}
```

When enabled, requests to the proactive endpoints must include a valid bearer token issued for one of the configured audiences.

## Further Reading

To learn more about building agents, see the [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
