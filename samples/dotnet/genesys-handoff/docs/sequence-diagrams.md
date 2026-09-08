# Genesys Handoff runtime sequence diagrams

This document provides detailed sequence diagrams for the major runtime flows in the Genesys Handoff sample and explicitly calls out what is persisted in storage.

## Storage model used by this sample

The sample uses `IStorage` (currently registered as `MemoryStorage`) as its persistence layer.

Logical records written by the app:

- Conversation reference
  - Key: `<mcsConversationId>`
  - Value: `ConversationReference` for proactive sends to Teams
  - Written by: `GenesysMessageSender.StoreUserChannelReferenceAsync`
  - Deleted by: `GenesysMessageSender.DeleteUserChannelReferenceAsync`

- Activity reply map
  - Key: `activity_reply_map_<mcsConversationId>`
  - Value: dictionary of `relayBotActivityId -> mcsActivityId`
  - Written by: `ActivityReplyMappingStore.UpsertAsync`
  - Deleted by: `ActivityReplyMappingStore.DeleteConversationMappingsAsync`

- Reset requested flag
  - Key: `conversation_reset_requested_<mcsConversationId>`
  - Value: `{ resetRequested: true }`
  - Written by: `ConversationResetService.MarkConversationResetRequestedAsync`
  - Cleared by: `ConversationResetService.CheckAndClearResetRequestedAsync`

- Agent disconnected flag
  - Key: `agent_disconnected_<mcsConversationId>`
  - Value: `{ disconnected: true }`
  - Written by: `GenesysNotificationService.HandleAgentDisconnectAsync`
  - Cleared by: `GenesysNotificationService.CheckAndClearAgentDisconnectedAsync`

- Genesys conversation registry
  - Key: `genesys_conversation_registry`
  - Value: dictionary of `genesysConversationId -> mcsConversationId`
  - Written/updated by: `ConversationMappingStore.AddAsync` and `ConversationMappingStore.RemoveAsync`
  - Loaded by: `ConversationMappingStore.LoadAsync`

Notes:

- Conversation state properties such as `IsEscalated`, `MCSConversationId`, `LastCopilotStudioReference`, and `GenesysConversationId` are managed by the Agent SDK conversation state infrastructure and used throughout these flows.

## 1) Teams user sends message to MCS through relay bot

```mermaid
sequenceDiagram
    autonumber
    participant U as "Teams User"
    participant T as "Teams Channel"
    participant A as "Relay Bot (GenesysHandoffAgent)"
    participant S as "IStorage"
    participant C as "Copilot Studio (MCS)"

    U->>T: Send message
    T->>A: Incoming activity

    A->>S: Write key [mcsConversationId] = ConversationReference
    Note right of A: StoreUserChannelReferenceAsync

    alt Teams activity contains replyToId
        A->>S: Read key activity_reply_map_[mcsConversationId]
        S-->>A: relayBotActivityId -> mcsActivityId map
        A->>A: Set outbound MCS ReplyToId to mapped mcsActivityId
    end

    A->>C: SendActivityAsync(activityToSend)
    C-->>A: Message response activity (with mcsActivityId)

    A->>T: Send response to Teams
    T-->>A: resourceResponse.Id (relayBotActivityId)

    A->>S: Upsert key activity_reply_map_[mcsConversationId][relayBotActivityId] = mcsActivityId
    Note right of A: Enables future Teams reply threading back to MCS
```

## 2) User requests escalation

```mermaid
sequenceDiagram
    autonumber
    participant U as "Teams User"
    participant A as "Relay Bot (GenesysHandoffAgent)"
    participant C as "Copilot Studio (MCS)"
    participant G as "Genesys Open Messaging API"
    participant N as "GenesysNotificationService"
    participant S as "IStorage"

    U->>A: Escalation intent message
    A->>C: Forward message
    C-->>A: Event: GenesysHandoff (with summary)

    A->>A: Set IsEscalated = true (conversation state)
    A->>G: SendMessageToGenesysAsync(prefetchConversationId=true)

    A->>S: Write key [mcsConversationId] = ConversationReference
    Note right of A: Store channel reference for proactive callbacks

    G-->>A: Return genesysConversationId
    A->>A: Set GenesysConversationId in conversation state

    opt Notifications enabled
        A->>N: SubscribeToConversationEventsAsync(genesysConversationId, mcsConversationId)
        N->>S: Upsert genesys_conversation_registry[genesysConversationId] = mcsConversationId
        N->>G: Subscribe to v2.detail.events.conversation.[id].user.end
    end
```

## 3) User sends message while connected to live agent

```mermaid
sequenceDiagram
    autonumber
    participant U as "Teams User"
    participant A as "Relay Bot (GenesysHandoffAgent)"
    participant N as "GenesysNotificationService"
    participant S as "IStorage"
    participant G as "Genesys Open Messaging API"

    U->>A: Message while IsEscalated = true

    A->>S: Read key agent_disconnected_[mcsConversationId]
    alt Agent disconnected flag exists
        A->>S: Delete key agent_disconnected_[mcsConversationId]
        A->>S: Delete key [mcsConversationId]
        A->>A: Clear conversation state and start new MCS conversation
    else No disconnect flag
        alt Message equals EndLiveChatMessage
            A->>A: Route to user disconnect flow (see Flow 6)
        else Regular live chat message
            A->>G: POST /conversations/messages/{integrationId}/inbound/open/message
            Note right of A: channel.messageId = Teams activity.Id
        end
    end
```

## 4) Live agent replies back to Teams

```mermaid
sequenceDiagram
    autonumber
    participant G as "Genesys Outbound Webhook"
    participant W as "GenesysWebhookHandler"
    participant S as "IStorage"
    participant T as "Teams Channel"

    G->>W: POST /api/outbound (payload.channel.to.id = mcsConversationId)
    W->>W: Validate X-Hub-Signature-256

    W->>S: Read key [mcsConversationId]
    S-->>W: ConversationReference

    alt payload.text is empty
        W->>T: Send typing activity proactively
    else payload has text
        W->>T: Send proactive message to Teams
        T-->>W: resourceResponse.Id (relayBotActivityId)

        W->>S: Upsert key activity_reply_map_[mcsConversationId][relayBotActivityId] = payload.channel.messageId
        Note right of W: payload.channel.messageId is mcsActivityId from Genesys payload
    end
```

## 5) Live agent disconnect event from Genesys

```mermaid
sequenceDiagram
    autonumber
    participant G as "Genesys Notifications WebSocket"
    participant N as "GenesysNotificationService"
    participant S as "IStorage"
    participant T as "Teams Channel"

    G-->>N: topic v2.detail.events.conversation.{genesysConversationId}.user.end

    N->>S: Remove genesys_conversation_registry[genesysConversationId]
    S-->>N: mcsConversationId

    N->>S: Read key [mcsConversationId]
    S-->>N: ConversationReference

    N->>S: Write key agent_disconnected_[mcsConversationId] = { disconnected: true }
    N->>T: Proactively notify user that live agent disconnected

    Note over N,S: On next user turn, agent reads and clears this flag
```

## 6) User explicitly disconnects from live agent

```mermaid
sequenceDiagram
    autonumber
    participant U as "Teams User"
    participant A as "Relay Bot (GenesysHandoffAgent)"
    participant G as "Genesys Open Messaging API"
    participant N as "GenesysNotificationService"
    participant S as "IStorage"
    participant C as "Copilot Studio (MCS)"

    U->>A: Click/send EndLiveChatMessage
    A->>G: DisconnectConversationAsync(genesysConversationId)
    Note right of A: GET conversation, then PATCH customer participant state=disconnected

    opt Notifications enabled
        A->>N: UnsubscribeFromConversationEventsAsync(genesysConversationId)
        N->>S: Remove genesys_conversation_registry[genesysConversationId]
    end

    A->>S: Delete key [mcsConversationId]
    A->>A: Clear conversation state
    A->>U: "You have ended the chat with the live agent."

    A->>C: StartConversationAsync(emitStartConversationEvent=true)
    C-->>A: New conversation reference
    A->>A: Save new MCS conversation state
```

## 7) Conversation reset API is called

```mermaid
sequenceDiagram
    autonumber
    participant API as "Client calling /api/conversations/reset"
    participant R as "ConversationResetService"
    participant S as "IStorage"
    participant T as "Teams Channel"
    participant A as "Relay Bot (next turn)"
    participant C as "Copilot Studio (MCS)"

    API->>R: ResetConversationAsync(mcsConversationId, optionalMessage)

    R->>S: Read key [mcsConversationId] (conversation state payload)
    R->>R: Check IsEscalated in persisted state

    alt IsEscalated == true
        R-->>API: 409 Cannot reset escalated conversation
    else Not escalated
        R->>S: Write key conversation_reset_requested_[mcsConversationId] = { resetRequested: true }

        opt message configured or provided
            R->>S: Read key [mcsConversationId] for ConversationReference
            R->>T: Send proactive reset message
        end

        R->>S: Delete key [mcsConversationId]
        R->>S: Delete key activity_reply_map_[mcsConversationId]
        R-->>API: 200 reset accepted
    end

    Note over A,S: On next incoming user turn
    A->>S: Read and delete conversation_reset_requested_[mcsConversationId]
    A->>A: Clear conversation state
    A->>C: Start new conversation
```

## Quick validation checklist

- Teams message forwarding stores `relayBotActivityId -> mcsActivityId` in `activity_reply_map_<mcsConversationId>`.
- Teams reply threading to MCS resolves `incomingActivity.replyToId` through that map before sending to MCS.
- Escalation and disconnect flows keep `genesys_conversation_registry` consistent.
- Reset API removes proactive reference and reply mapping records and sets a deferred reset marker.
