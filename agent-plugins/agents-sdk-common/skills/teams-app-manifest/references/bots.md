# Bots and agent messaging

Use this reference when source evidence includes a Teams bot or Agents SDK Teams routes.

Primary sources:

- [Bots manifest object](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/root-bots)
- [Bot command lists object](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/root-bots-command-lists)
- [Copilot agents manifest object](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/root-copilot-agents)
- [Microsoft 365 app model for agents](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/agents-are-apps)
- [Teams app permissions and consent](https://learn.microsoft.com/en-us/microsoftteams/app-permissions)
- [Build bots for Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/what-are-bots)
- [Expose slash commands from agents and apps](https://learn.microsoft.com/en-us/microsoftteams/platform/agents-in-teams/agent-slash-commands)
- [Send and receive targeted messages](https://learn.microsoft.com/en-us/microsoftteams/platform/agents-in-teams/targeted-messages)
- [Send and receive files](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/bots-filesv4)
- [Send proactive messages](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/send-proactive-messages)

## Base bot declaration

Evidence includes an Agents SDK `AgentApplication` that is exposed through Teams, `[TeamsExtension]`, Teams route attributes, or equivalent Teams activity handlers. A base declaration uses `bots[].botId` and `bots[].scopes`.

Do not infer scopes from a generic message handler. Require product documentation, tests, an existing manifest, or explicit intent. A handler can receive several conversation types without the product intending installation in all of them.

`commandLists` are user-visible discovery metadata. Add only documented user commands. Do not convert every regex route, hidden diagnostic command, card action verb, or invoke handler into a command.

A generic or dynamic runtime command handler does not make discovery metadata unnecessary. When code and user-facing documentation present a named slash or mention command as part of the intended Teams experience, evaluate and declare that command using the rules below. Conversely, a route alone remains insufficient evidence that a command should be advertised.

Record command discovery as its own capability-ledger item; do not merge named commands into a generic message-routing entry. Reconcile four distinct questions: whether code handles the command, whether user-facing evidence intends it to be discoverable, where it is supported, and which Teams entry points should expose it. An existing manifest omission answers none of those questions. If code and original user-facing documentation establish discovery intent, repair the manifest rather than deleting that intent, unless stronger evidence proves the documentation wrong or the capability unsupported.

## Command-list grouping

`commandLists[].scopes` and `commandLists[].triggers` apply to every command in that command-list entry; they do not apply to an individual command.

- Determine the complete scope set and trigger set for each command before creating any command-list entry.
- Normalize each command's applicability as `(sorted scopes, sorted triggers)`. Create one command-list entry for each unique applicability pair and put all commands with that pair in the entry.
- Put every applicable scope and trigger in that entry and declare each command exactly once. Never repeat a command across narrower groups when one group with its complete scope and trigger set represents it.
- Create another command-list entry only when a command requires a different scope or trigger set.
- Grouping exists only to reduce duplicate command declarations. Never add a scope or trigger to make commands fit the same group, because that broadens where those commands are exposed.
- Omit `triggers` for mention-only behavior when the selected released schema defines `mention` as the default. Emit it when repository convention requires explicit values.
- If exact grouping exceeds the selected schema's command-list limit, report `needs-input`; do not merge entries and broaden command exposure.

For example, if Command A is slash-only, Command B is mention-only, and Command C supports both triggers with the same scopes, create three entries and declare each command once:

```json
[
  {
    "scopes": ["personal", "team", "groupChat"],
    "triggers": ["slash"],
    "commands": [{ "title": "command-a", "description": "Run Command A." }]
  },
  {
    "scopes": ["personal", "team", "groupChat"],
    "triggers": ["mention"],
    "commands": [{ "title": "command-b", "description": "Run Command B." }]
  },
  {
    "scopes": ["personal", "team", "groupChat"],
    "triggers": ["slash", "mention"],
    "commands": [{ "title": "command-c", "description": "Run Command C." }]
  }
]
```

## Command scopes

`bots[].scopes` declares where the bot can be installed. `commandLists[].scopes` declares where a specific command is discoverable. A command's scopes must be a supported subset of the bot scopes, but they are a separate decision.

- Include every bot scope where the command handler and documented product intent support that command.
- A conversation-agnostic handler plus documentation that commands work across the bot's scopes is strong evidence to use those bot scopes for the command.
- Do not remove `personal` only because targeted messages are limited to group conversations. Named slash and mention command configuration supports `personal`; evaluate the command behavior independently.
- Exclude a scope only when source behavior, documentation, or explicit intent limits the command there. For example, a command that summarizes a channel requires shared-conversation context and should not be exposed in `personal`.

## Root permissions

Root `permissions` is separate from `authorization.permissions.resourceSpecific`, Microsoft Graph permissions, and Entra consent. It can contain only `identity` and `messageTeamMembers`. Do not add either value to every bot.

- Add `identity` when code or explicit requirements retrieve or use Teams member identity information such as member details, UPN, email, or Microsoft Entra object ID. An opaque sender ID used only to reply to the current activity is insufficient evidence.
- Add `messageTeamMembers` when code or explicit requirements let the app initiate direct or proactive messages to team members who have not first interacted with the bot. A normal reply or a continuation sent only to a user who already interacted is insufficient evidence.
- Add both only when both behaviors are proven.
- An example manifest containing both values is not evidence that the current application requires them.
- In Complete mode, preserve existing root permissions and flag any value not supported by source or requirements.

## Feature decisions

| Feature | Strong source evidence | Manifest impact | Decision rule |
|---|---|---|---|
| Standard messages | Teams message routes or handlers | Base `bots` entry | Scopes require separate evidence |
| Proactive messages | Stored conversation reference plus proactive continuation/send | No dedicated proactive flag | Preserve required bot scopes; document installation and conversation-reference requirements |
| One-way notifications | Application only sends notifications and accepts no conversational input | `isNotificationOnly: true` | Explicit product intent required; proactive code alone is insufficient |
| Targeted messages | Targeted-message send/receive APIs or explicit feature requirement | Schema 1.29 or later: `supportsTargetedMessages: true` | Do not enable for normal replies or standard proactive messages; a lower schema requires an explicit upgrade decision |
| Teams file consent | File consent accept/decline routes or file-consent cards | `supportsFiles: true` | Teams file-consent APIs require `personal` scope; Graph file APIs do not by themselves prove this flag |
| Adaptive Cards | Card attachments or `Action.Execute` routes | No card-specific bot flag | Keep card behavior in code; add other fields only when the card opens a URL dialog or requires SSO |
| AI labels, citations, feedback, sensitivity metadata | AI entities or feedback routes | Usually no additional manifest field | Do not invent a capability flag |
| Calling | Calling bot implementation and registration requirement | `supportsCalling: true` | Explicit requirement and external bot configuration required |
| Video | Video bot implementation and registration requirement | `supportsVideo: true` | Explicit requirement and external bot configuration required |
| Copilot custom-engine exposure | Explicit requirement to expose the bot in Copilot | `copilot` scope plus matching `copilotAgents.customEngineAgents` | Agents SDK usage alone does not prove Copilot exposure |

## Targeted messages

`supportsTargetedMessages` and command-list `triggers` first appear in released schema 1.29. Validate against that exact schema or a later approved released schema. Do not place these fields for 1.28 or lower manifest schema version.

When the feature and schema are verified, add the flag to the existing bot object:

```json
{
  "supportsTargetedMessages": true
}
```

Also verify that declared scopes match where the targeted-message feature is intended to work. Command triggers such as slash or mention are separate `commandLists` decisions.

Any command list that uses a `slash` trigger requires `supportsTargetedMessages: true`. Do not add slash triggers to a standard bot command list.

Schema source: [schema v1.29 release](https://github.com/OfficeDev/microsoft-teams-app-schema/releases/tag/v1.29).

## Files

For Teams file-consent APIs, add:

```json
{
  "supportsFiles": true,
  "scopes": ["personal"]
}
```

Do not copy this fragment unchanged if the bot also supports other proven scopes. `supportsFiles` applies to the bot object, while the file-consent workflow itself remains personal-only. If code uses Microsoft Graph to work with OneDrive or SharePoint files, evaluate authentication and permissions instead of automatically enabling `supportsFiles`.

## Proactive messaging

There is no general `supportsProactiveMessages` property. Verify:

- The app can be installed in every intended scope.
- The code stores the required conversation reference or identifiers.
- Any proactive installation through Graph is documented as external configuration.
- `isNotificationOnly` remains false unless the entire bot is intentionally one-way.

## Copilot custom-engine agents

`copilotAgents` declares agents exposed in Microsoft 365 Copilot. It is not a general agentic-AI flag and does not identify the authoring tool. `declarativeAgents` references a declarative agent definition; `customEngineAgents` declares a custom-engine agent experience.

Do not add `copilotAgents` only because an app uses Agents SDK, implements agentic behavior, or was authored with Copilot Studio. Require explicit product intent for Microsoft 365 Copilot exposure. When a custom-engine agent is explicitly required, verify the selected released schema and couple the bot `copilot` scope with the matching custom-engine agent declaration while retaining `personal` scope as required for the custom-engine agent experience. Treat preview or staged-rollout properties as unsupported unless the user explicitly accepts them.
