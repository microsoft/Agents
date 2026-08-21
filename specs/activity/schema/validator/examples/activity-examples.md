# Activity Protocol Examples

This file contains example JSON payloads for each of the activity types defined in the `activity-protocol.schema.json`.

## 1. Message

A `message` activity represents content intended to be shown within a conversational interface.

```json
{
  "type": "message",
  "id": "message-activity-1",
  "timestamp": "2025-09-15T10:00:01.000Z",
  "localTimestamp": "2025-09-15T03:00:01.000-07:00",
  "localTimezone": "America/Los_Angeles",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "isGroup": true, "id": "conv-1", "name": "Project Discussion" },
  "recipient": { "id": "bot-id-1", "name": "HelpBot" },
  "text": "Hello, I need help with my order.",
  "textFormat": "plain",
  "locale": "en-US",
  "attachments": [
    {
      "contentType": "application/vnd.microsoft.card.hero",
      "content": {
        "title": "Order #12345",
        "text": "Status: Shipped"
      }
    }
  ],
  "suggestedActions": {
    "actions": [
      {
        "type": "imBack",
        "title": "Track Package",
        "value": "track order 12345"
      },
      {
        "type": "imBack",
        "title": "Cancel Order",
        "value": "cancel order 12345"
      }
    ]
  }
}
```

## 2. Contact Relation Update

A `contactRelationUpdate` activity signals a change in the relationship between the bot and a user.

```json
{
  "type": "contactRelationUpdate",
  "id": "contact-relation-update-1",
  "timestamp": "2025-09-15T10:00:02.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "skype",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-2" },
  "recipient": { "id": "bot-id-1", "name": "WelcomeBot" },
  "action": "add"
}
```

## 3. Conversation Update

A `conversationUpdate` activity describes a change in a conversation's members, description, or other properties.

```json
{
  "type": "conversationUpdate",
  "id": "conversation-update-1",
  "timestamp": "2025-09-15T10:00:03.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-2", "name": "Jane Smith" },
  "conversation": { "isGroup": true, "id": "conv-1", "name": "Project Discussion" },
  "recipient": { "id": "bot-id-1", "name": "HelpBot" },
  "membersAdded": [
    { "id": "user-id-3", "name": "Sam Brown" }
  ],
  "membersRemoved": [],
  "topicName": "Project Discussion Kickoff"
}
```

## 4. Typing

A `typing` activity indicates that a user or bot is typing.

```json
{
  "type": "typing",
  "id": "typing-1",
  "timestamp": "2025-09-15T10:00:04.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "webchat",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-3" },
  "recipient": { "id": "bot-id-1", "name": "EchoBot" }
}
```

## 5. End of Conversation

An `endOfConversation` activity signals the end of a conversation.

```json
{
  "type": "endOfConversation",
  "id": "end-of-conv-1",
  "timestamp": "2025-09-15T10:00:05.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "bot-id-1", "name": "SurveyBot" },
  "conversation": { "id": "conv-4" },
  "recipient": { "id": "user-id-4", "name": "Peter Jones" },
  "code": "completedSuccessfully",
  "text": "Thanks for completing the survey!"
}
```

## 6. Event

An `event` activity communicates programmatic information from a client or channel to a bot.

```json
{
  "type": "event",
  "id": "event-1",
  "timestamp": "2025-09-15T10:00:06.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "webchat",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-5" },
  "recipient": { "id": "bot-id-1", "name": "LocationBot" },
  "name": "locationReceived",
  "value": {
    "latitude": 47.6062,
    "longitude": -122.3321
  }
}
```

## 7. Invoke

An `invoke` activity is a request/response style activity that communicates programmatic information between a client/channel and a bot.

```json
{
  "type": "invoke",
  "id": "invoke-1",
  "timestamp": "2025-09-15T10:00:07.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-6" },
  "recipient": { "id": "bot-id-1", "name": "TaskBot" },
  "name": "task/submit",
  "value": {
    "taskId": "task-987",
    "data": {
      "comments": "All done."
    }
  }
}
```

## 8. Message Update

A `messageUpdate` activity represents an update of an existing message activity.

```json
{
  "type": "messageUpdate",
  "id": "message-activity-1",
  "timestamp": "2025-09-15T10:00:08.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-1" },
  "recipient": { "id": "bot-id-1", "name": "HelpBot" },
  "text": "Hello, I need help with my order #12345.",
  "locale": "en-US"
}
```

## 9. Message Delete

A `messageDelete` activity represents a deletion of an existing message activity.

```json
{
  "type": "messageDelete",
  "id": "message-activity-to-delete",
  "timestamp": "2025-09-15T10:00:09.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-1", "name": "John Doe" },
  "conversation": { "id": "conv-1" },
  "recipient": { "id": "bot-id-1", "name": "HelpBot" }
}
```

## 10. Installation Update

An `installationUpdate` activity represents the installation or uninstallation of a bot.

```json
{
  "type": "installationUpdate",
  "id": "install-update-1",
  "timestamp": "2025-09-15T10:00:10.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-admin", "name": "Admin User" },
  "conversation": { "id": "conv-tenant-level" },
  "recipient": { "id": "bot-id-1", "name": "AdminBot" },
  "action": "add"
}
```

## 11. Message Reaction

A `messageReaction` activity represents a social interaction on an existing message.

```json
{
  "type": "messageReaction",
  "id": "reaction-1",
  "timestamp": "2025-09-15T10:00:11.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "msteams",
  "from": { "id": "user-id-2", "name": "Jane Smith" },
  "conversation": { "id": "conv-1" },
  "recipient": { "id": "bot-id-1", "name": "HelpBot" },
  "replyToId": "message-activity-1",
  "reactionsAdded": [
    { "type": "like" }
  ],
  "reactionsRemoved": []
}
```

## 12. Suggestion

A `suggestion` activity allows a bot to suggest content to a single user that augments a previous activity.

```json
{
  "type": "suggestion",
  "id": "suggestion-1",
  "timestamp": "2025-09-15T10:00:12.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "cortana",
  "from": { "id": "bot-id-1", "name": "CalendarBot" },
  "conversation": { "id": "conv-7" },
  "recipient": { "id": "user-id-1", "name": "John Doe" },
  "replyToId": "original-message-id",
  "text": "I can create that meeting for you.",
  "textHighlights": [
    {
      "text": "meet on Monday",
      "occurrence": 1
    }
  ]
}
```

## 13. Trace

A `trace` activity is used for logging and debugging purposes. It is typically not shown to the user.

```json
{
  "type": "trace",
  "id": "trace-1",
  "timestamp": "2025-09-15T10:00:13.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "emulator",
  "from": { "id": "bot-id-1", "name": "DebuggingBot" },
  "conversation": { "id": "conv-debug" },
  "recipient": { "id": "user-id-dev", "name": "Developer" },
  "name": "LUIS_TRACE",
  "label": "LUIS Trace",
  "valueType": "https://www.luis.ai/schemas/trace",
  "value": {
    "query": "book a flight to paris",
    "topScoringIntent": {
      "intent": "BookFlight",
      "score": 0.98
    },
    "entities": [
      { "type": "Location", "entity": "paris" }
    ]
  }
}
```

## 14. Handoff

A `handoff` activity is used to request or signal a change in focus between elements inside a bot, such as handing off to a human agent.

```json
{
  "type": "handoff",
  "id": "handoff-1",
  "timestamp": "2025-09-15T10:00:14.000Z",
  "serviceUrl": "https://smba.trafficmanager.net/amer/",
  "channelId": "custom-channel",
  "from": { "id": "bot-id-1", "name": "TriageBot" },
  "conversation": { "id": "conv-8" },
  "recipient": { "id": "human-agent-system", "name": "Live Agent Hub" },
  "value": {
    "reason": "User requested human agent",
    "transcript": [
      "User: I need to speak to a person.",
      "Bot: OK, I'll connect you to a live agent."
    ]
  }
}
```

## 15. Simple

```json
{
  "type": "message",
  "channelId": "emulator",
  "from": {
    "id": "user1",
    "name": "User One"
  },
  "conversation": {
    "id": "conv1",
    "name": "Conversation 1"
  },
  "recipient": {
    "id": "bot",
    "name": "Bot"
  },
  "serviceUrl": "https://example.org",
  "text": "Hello, world!"
}
```
