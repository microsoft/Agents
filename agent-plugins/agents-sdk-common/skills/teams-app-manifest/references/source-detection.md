# Source detection

Use source evidence to propose capabilities. Do not treat detection as authorization to enable a feature, scope, permission, or public surface.

Search the full project, including route classes, startup registration, services, card payloads, configuration, tests, and README files. A route can be registered indirectly or without an attribute.

## .NET Agents SDK signals

| Source signal | Candidate capability | Read next |
|---|---|---|
| `[TeamsMessageRoute]`, Teams members or conversation routes | Conversational bot | [bots.md](bots.md) |
| Named commands presented in README usage, help responses, command menus, or explicit slash/@mention UX, together with handling code | User-facing command discovery | [bots.md](bots.md) |
| Proactive continuation APIs plus stored conversation data | Proactive messaging | [bots.md](bots.md) |
| Member-detail APIs or use of UPN, email, Microsoft Entra object ID, or other Teams member identity data | Candidate root `identity` permission | [bots.md](bots.md) |
| Member enumeration plus direct or proactive messaging to team members who have not interacted with the bot | Candidate root `messageTeamMembers` permission | [bots.md](bots.md) |
| Targeted-message APIs or targeted-message activity handling | Targeted messages | [bots.md](bots.md) |
| `[TeamsFileConsentAcceptRoute]`, `[TeamsFileConsentDeclineRoute]`, file-consent card | Teams file consent | [bots.md](bots.md) |
| Adaptive Card attachment | Adaptive Cards | [cards-and-dialogs.md](cards-and-dialogs.md) |
| `[ActionExecuteRoute]` | Adaptive Card action | [cards-and-dialogs.md](cards-and-dialogs.md) |
| `[TeamsTaskFetchRoute]`, `[TeamsTaskSubmitRoute]` | Bot dialog/task module | [cards-and-dialogs.md](cards-and-dialogs.md) |
| `[TeamsQueryRoute]`, `[TeamsSelectItemRoute]` | Search message extension | [message-extensions.md](message-extensions.md) |
| `[TeamsSubmitActionRoute]`, `[TeamsFetchActionRoute]` | Action message extension | [message-extensions.md](message-extensions.md) |
| `[TeamsQueryLinkRoute]`, `[TeamsAnonQueryLinkRoute]` | Link unfurling | [message-extensions.md](message-extensions.md) |
| `[TeamsConfigFetchRoute]`, `[TeamsConfigSubmitRoute]` | Configurable message extension | [message-extensions.md](message-extensions.md) |
| `[TeamsMeetingStartRoute]`, `[TeamsMeetingEndRoute]` | Meeting lifecycle events | [meetings.md](meetings.md) |
| `[TeamsMeetingParticipantsJoinRoute]`, `[TeamsMeetingParticipantsLeaveRoute]` | Meeting participant events | [meetings.md](meetings.md) |
| Teams meeting client calls | Meeting details or notification APIs | [meetings.md](meetings.md) |
| Microsoft Graph online-meeting or transcript client | Graph meeting access | [meetings.md](meetings.md) and [authentication-and-permissions.md](authentication-and-permissions.md) |
| User token acquisition or on-behalf-of Graph client | SSO/delegated access | [authentication-and-permissions.md](authentication-and-permissions.md) |
| AI entities, citations, feedback, or sensitivity metadata | Teams AI message presentation | [bots.md](bots.md) |

## Detection rules

- A class or package reference is weak evidence; require executed code or explicit documentation.
- A handler proves accepted input, not intended installation scope.
- Runtime message dispatch and manifest command discovery are separate decisions. Inventory documented named commands separately from the generic conversational-bot capability.
- A named route alone may be hidden or internal. Require user-facing product intent before advertising it; when help text or README instructions present it as slash or @mention UX, do not use text matching as evidence that discovery metadata is unnecessary.
- An SDK type can support several manifest shapes; inspect actual values and branches.
- A public route proves an endpoint exists, not that its domain is approved for the manifest.
- A Graph permission string in documentation is evidence of setup intent, not proof of granted consent.
- Root `permissions`, RSC, Graph permissions, and Entra consent are separate decisions. Never transfer evidence between them.
- A reply to the current activity does not prove `messageTeamMembers`; an opaque sender ID used only for routing does not prove `identity`.
- README setup instructions can supply product intent, but validate them against code and released documentation.
- Existing manifests are evidence, not authority. Preserve unexplained fields in Complete mode and flag them.

## Non-attribute implementations

For inline registration, middleware routing, or another language, map behavior by activity name and payload rather than by class name. Use the same feature references. Add language-specific signals only after verifying the released SDK surface for that language.

## Negative evidence

Record meaningful absence when it limits a decision. Examples:

- File-consent routes exist but `personal` scope is not documented.
- A URL dialog exists but the URL comes from missing configuration.
- A message-extension handler exists but title, description, context, or parameters are absent.
- Meeting routes exist but private versus channel meeting support is unclear.
- Graph calls exist but delegated versus application access is unclear.

These cases must produce `needs-input`, not guessed JSON.
