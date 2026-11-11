# Message extensions

Primary sources:

- [Build message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/what-are-messaging-extensions)
- [Define search commands](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/search-commands/define-search-command)
- [Build bot-based message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/build-bot-based-message-extension)
- [Link unfurling](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/link-unfurling)

Message extensions require `composeExtensions`. Bot-based extensions also require a bot registration and matching `botId`.

## Detection mapping

| Source evidence | Manifest declaration |
|---|---|
| Query route with command ID | Command with matching `id` and `type: query` |
| Submit/fetch action route with command ID | Command with matching `id` and `type: action` |
| Query-link route | `messageHandlers` entry with `type: link` and controlled domains |
| Select-item route | Part of the related query command; do not create a separate command |
| Configuration fetch/submit routes | `canUpdateConfiguration: true` plus required configuration behavior |

The route command ID and manifest command ID must match exactly. Derive titles, descriptions, contexts, parameters, and input types from product documentation, UI text, existing manifests, or explicit requirements. Code can prove an ID but often cannot prove good user-facing metadata.

## Search command shape

```json
{
  "composeExtensions": [
    {
      "botId": "${{BOT_ID}}",
      "commands": [
        {
          "id": "<verified-command-id>",
          "type": "query",
          "title": "<required-user-facing-title>",
          "description": "<required-user-facing-description>",
          "context": ["compose", "commandBox"],
          "parameters": []
        }
      ]
    }
  ]
}
```

Do not copy the contexts or empty parameters without verifying the implementation and intended user experience.

## Link unfurling shape

```json
{
  "messageHandlers": [
    {
      "type": "link",
      "value": {
        "domains": ["<controlled-domain>"]
      }
    }
  ]
}
```

Use only domains controlled by the application owner. Do not add broad public suffixes or a domain merely because source code can fetch it. Distinguish domains monitored for link unfurling from application hosts in `validDomains`.

## Merge rules

- One app has one message-extension definition, which can contain several verified commands.
- Merge query, action, configuration, and link handlers into that definition.
- Preserve existing commands not found in current code and flag them for review.
- Do not create a message extension because a normal bot sends cards.
