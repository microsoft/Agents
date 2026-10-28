# Teams capability source map

Use this file as a research index. Do not treat it as a manifest template.

Primary indexes:

- [Teams capabilities mapped to features](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/design/map-use-cases#app-capabilities-mapped-to-features)
- [Microsoft 365 app manifest schema](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/)
- [Create a Teams app package](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/apps-package)

Classification:

- **Required**: the feature needs a Teams manifest field.
- **Conditional**: the field depends on the implementation, scope, identity model, or permission model.
- **None**: the feature has no feature-specific Teams manifest field. Its host capability must still be declared.
- **Stop**: do not generate a value until the stated condition is resolved.

General rules:

1. Select only a generally available schema version approved by the repository.
2. Validate every property against that exact schema. A property in newer or preview documentation is not permission to upgrade.
3. Do not infer installation scopes, Entra IDs, domains, authentication, or RSC permissions from weak evidence.
4. Graph application/delegated permissions normally belong to the Microsoft Entra app registration. Only RSC permissions belong in `authorization.permissions.resourceSpecific`.
5. A valid manifest does not prove that it matches the code.

## Tab

Base source: [Build tabs for Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/what-are-tabs).

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| Display Power BI data | Conditional | Standard tab declaration: `staticTabs` or `configurableTabs`; its URL fields; `validDomains` | Not stated | [Build a dashboard tab app](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/how-to/build-a-dashboard-tab-app). If the sample only adds Microsoft's Power BI tab, do not create a custom app manifest entry. |
| Personal tab across Microsoft 365 | Required | `staticTabs[]`; `staticTabs[].scopes` containing `personal`; URL fields; `validDomains` | 1.13 for extension across Microsoft 365 | [Extend agents and apps across Microsoft 365](https://learn.microsoft.com/en-us/microsoftteams/platform/m365-apps/overview). Stop if target hosts or runtime requirements are not stated. |
| Deep links | Conditional | No dedicated deep-link field. The target capability must exist; a URL supplied in an app deep link can require its domain in `validDomains` | Not stated | [Deep link to an application](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/deep-link-application). Do not create a tab only because code contains a Teams deep link. |
| Display dialogs | Conditional | URL-based dialog domains in `validDomains`; the invoking tab/bot/message extension declaration | Not stated | [Dialogs](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/what-are-task-modules), [invoke dialogs in Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/task-modules/invoking-task-modules). Adaptive Card dialogs do not add a dialog-specific manifest field. |
| SSO and third-party authentication | Conditional | SSO: `webApplicationInfo.id`, `webApplicationInfo.resource`; relevant URLs and domains. Third-party OAuth commonly affects `validDomains` | 1.5 for `webApplicationInfo` | [Tab SSO manifest](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/how-to/authentication/tab-sso-manifest), [third-party OAuth](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/how-to/authentication/auth-flow-tab). Stop until identity provider, Entra app ID, app ID URI, and redirect domains are known. |
| Display `<iframe>` content | Required | `staticTabs` or `configurableTabs`; `contentUrl` or `configurationUrl`; `websiteUrl` when applicable; `validDomains` | Not stated | [Build tabs for Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/what-are-tabs), [tab content page](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/how-to/create-tab-pages/content-page). |
| Display SharePoint pages and web parts | Conditional | Teams package uses a tab declaration; SPFx also uses its own component manifest `supportedHosts`, which is not `manifest.json` | Not stated | [Add Teams tab to SharePoint](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/how-to/tabs-in-sharepoint), [expose SPFx web parts in Teams](https://learn.microsoft.com/en-us/sharepoint/dev/spfx/build-for-teams-expose-webparts-teams). Stop if the project is not an SPFx project. |
| Request device permissions | Required | Root `devicePermissions` with only used values such as `media`, `geolocation`, `notifications`, `midi`, or `openExternal` | Not stated | [Integrate media capabilities](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/device-capabilities/media-capabilities), [device permissions](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/device-capabilities/browser-device-permissions). Ask for each permission; do not add all values. |
| Display Power Apps | Conditional | Standard tab declaration only when packaging a custom personal/tab app | Not stated | [Power Apps and Teams](https://learn.microsoft.com/en-us/power-apps/teams/overview). If users add the Microsoft Power Apps tab, do not create a custom capability. |
| Tabs on mobile | Conditional | `websiteUrl` is required for mobile access in the documented case; normal tab URL/domain fields also apply | Not stated | [Plan tabs for mobile](https://learn.microsoft.com/en-us/microsoftteams/platform/tabs/design/tabs-mobile). Mobile support is not a separate capability object. |

## Bot

Base source: [Conversations with an agent](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/build-conversational-capability). Every bot needs a `bots[]` entry with `botId` and explicit `scopes`.

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| Send proactive messages | None | No proactive-message field. The bot's `bots[].scopes` must match the installed conversation | Not stated | [Proactive messages](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/send-proactive-messages). App installation and stored conversation identity are runtime requirements. Do not set `isNotificationOnly` unless the bot is one-way only. |
| Send cards | None | No card-specific Teams manifest field | Not stated | [Conversations with an agent](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/build-conversational-capability), [card reference](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-reference). Validate the Adaptive Card separately. |
| Update messages | None | No feature-specific field | Not stated | [Update and delete agent messages](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/build-conversational-capability). Runtime API behavior. |
| Delete messages | None | No feature-specific field | Not stated | [Update and delete agent messages](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/build-conversational-capability). Runtime API behavior. |
| Send files to users | Conditional | Teams bot file-consent flow: `bots[].supportsFiles: true` and `personal` in `bots[].scopes`; Graph-based file messages use Graph permissions instead | Not stated | [Send and receive files](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/bots-filesv4). Stop until Graph versus file-consent flow is known. |
| App-hosted media | Required | `bots[].supportsCalling: true`; add `bots[].supportsVideo: true` only for video | Not stated | [Register a calls and meetings bot](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/calls-and-meetings/registering-calling-bot), [application-hosted media requirements](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/calls-and-meetings/requirements-considerations-application-hosted-media-bots). This also needs Graph permissions and supported hosting; manifest alone is insufficient. |
| Display dialogs | Conditional | URL dialog domains in `validDomains`; no extra field for Adaptive Card dialog | Not stated | [Invoke dialogs in Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/task-modules/invoking-task-modules). |
| Get user, group chat, and team details | Conditional | Basic Teams context has no extra feature field. Broader member/message access can require `permissions`, Entra permissions, or RSC in `authorization.permissions.resourceSpecific` | 1.12 for current RSC form | [Teams-specific context](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/get-teams-context), [permissions in Teams apps](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/app-permissions/teams-app-permissions). Stop until the exact API and least privilege are known. |
| SSO and third-party authentication | Conditional | SSO: `webApplicationInfo.id`, `webApplicationInfo.resource`; Bot Framework token exchange domain in `validDomains` | 1.5 for `webApplicationInfo` | [Bot and message extension SSO manifest](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/authentication/bot-sso-manifest). Stop until Entra registration, resource URI, OAuth connection, and scopes are known. |
| Calling and meeting | Required | `bots[].supportsCalling`; `bots[].supportsVideo` if applicable; relevant bot scopes; Graph/RSC permissions where required | Not stated | [Register a calls and meetings bot](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/calls-and-meetings/registering-calling-bot). Do not enable calling based only on normal meeting-chat message handlers. |
| Send interactive notifications | Conditional | A normal interactive bot needs no extra field. A strictly one-way bot can use `bots[].isNotificationOnly: true` | Not stated | [App notification types](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/design/design-app-notification), [notification-only bot definition](https://learn.microsoft.com/en-us/microsoftteams/platform/get-started/glossary). Setting `isNotificationOnly` disables user messages and mentions; require explicit intent. |
| Targeted/private messages in group conversations | Required to receive | `bots[].supportsTargetedMessages: true`; matching `bots[].scopes`; optional `bots[].commandLists[].triggers` for slash commands | 1.29 | [Targeted messages](https://learn.microsoft.com/en-us/microsoftteams/platform/agents-in-teams/targeted-messages), [schema v1.29 release](https://github.com/OfficeDev/microsoft-teams-app-schema/releases/tag/v1.29). This is newer than the pictured feature map. Schemas 1.22 through 1.28 reject these properties. |

## Message extension

Base source: [Build message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/what-are-messaging-extensions). Bot-based message extensions use `composeExtensions[]` and a registered bot identity.

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| SSO and third-party authentication | Conditional | Bot-based SSO: `webApplicationInfo`; relevant `validDomains`. API-based SSO: `composeExtensions[].authorization.authType`, `.microsoftEntraConfiguration.supportsSingleSignOn`, plus `webApplicationInfo` | 1.5 for `webApplicationInfo`; API-based message extension requires 1.17 | [Bot/message-extension SSO](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/authentication/bot-sso-manifest), [API-based SSO](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/api-based-microsoft-entra). Stop until extension type and auth model are known. |
| Take action on messages | Required | `composeExtensions[].commands[]`; command `type: "action"`; `context` containing `message` when invoked from a message; `fetchTask`, `parameters`, and other command fields as the flow needs | `type`, `fetchTask`, and `context`: 1.4 | [Define action commands](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/action-commands/define-action-command), [build bot-based message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/build-bot-based-message-extension). |
| Send cards | None | No additional field beyond the message-extension declaration and command that returns the card | Not stated | [Build message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/what-are-messaging-extensions). Card payload is runtime output. |
| Unfurl links | Required | `composeExtensions[].messageHandlers[]`; handler `type: "link"`; `value.domains[]`; bot identity for bot-based implementation | Not stated | [Link unfurling](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/link-unfurling). Do not put arbitrary discovered domains in the manifest. |
| Display dialogs | Conditional | Action command in `composeExtensions[].commands[]`; `fetchTask` or parameters based on the flow; URL domains in `validDomains` | 1.4 for action command flow fields | [Define action commands](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/action-commands/define-action-command). Stop until static parameters, Adaptive Card, or URL dialog is known. |
| Search and display external data | Required | `composeExtensions[].commands[]`; command `type: "query"`; explicit `parameters`, `context`, and related fields | Core commands 1.0; command `type` 1.4 | [Define search commands](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/search-commands/define-search-command), [bot-based message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/build-bot-based-message-extension). API-based variant additionally needs `composeExtensionType`, `apiSpecificationFile`, and each command's `apiResponseRenderingTemplateFile` (1.17). |

## Adaptive Card

Base source: [Teams card reference](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-reference). Most entries change the Adaptive Card payload, not the Teams app manifest.

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| Card actions | Conditional | Usually none. `Action.OpenUrl` target domain must be in `validDomains`; the hosting bot/message extension must be declared | Not stated | [Card actions](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-actions). Card action types belong to the Adaptive Card JSON. |
| People Picker | None | No feature-specific Teams manifest field | Not stated | [People Picker in Adaptive Cards](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/people-picker). Configuration is in `Input.ChoiceSet` and `choices.data`. |
| Typeahead search | Conditional | Static search has no manifest effect. Dynamic bot search needs the bot installed in the target scope; for group chat, include `groupChat` in `bots[].scopes` | Not stated | [Typeahead search](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/dynamic-search). Card configuration is in `Input.ChoiceSet` and `Data.Query`. |
| SSO and third-party authentication | Conditional | Uses the hosting bot/tab/message-extension authentication configuration; commonly `webApplicationInfo` and auth domains | 1.5 for `webApplicationInfo` | [SSO for Adaptive Card Universal Actions](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/universal-actions-for-adaptive-cards/sso-adaptive-cards-universal-action). A card does not own a separate identity configuration. |
| Link unfurling | Required when this is a message-extension feature | `composeExtensions[].messageHandlers[]` with link domains | Not stated | [Link unfurling](https://learn.microsoft.com/en-us/microsoftteams/platform/messaging-extensions/how-to/link-unfurling). Stop if code only uses `Action.OpenUrl`; that is not link unfurling. |

## Apps for meetings

Base sources: [Apps for Teams meetings and calls](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/teams-apps-in-meetings) and [build tabs for meetings](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/build-tabs-for-meeting).

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| Integrate tabs, bots, and message extensions | Conditional | Declare each used capability. Meeting tabs use `configurableTabs[].scopes` and `configurableTabs[].context`; bots use `bots`; extensions use `composeExtensions` | Not stated | [Apps for Teams meetings and calls](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/teams-apps-in-meetings). Do not add all three without source evidence. |
| Get meeting events | Required | `webApplicationInfo`; RSC under `authorization.permissions.resourceSpecific`, selected by meeting type. Verified names include `OnlineMeeting.ReadBasic.Chat`, `ChannelMeeting.ReadBasic.Group`, and for participant events `OnlineMeetingParticipant.Read.Chat` | 1.12 for current RSC form | [Meeting apps APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis). Stop until private versus channel meeting and event types are known. Developer Portal event subscriptions are also required. |
| Scenes for meetings | Stop | Historical schema property: `meetingExtensionDefinition.scenes[]` | Not applicable for new generation | [Teams developer announcements](https://learn.microsoft.com/en-us/microsoftteams/platform/developer-announcements) states custom Together Mode and Scene Studio retired on June 30, 2026. Do not generate a scene configuration for a new app even if older schemas still contain it. |
| Customized meeting apps | Required | `configurableTabs[].scopes` (`groupchat` and/or `team`); `configurableTabs[].context` using only needed values: `meetingChatTab`, `meetingDetailsTab`, `meetingSidePanel`, `meetingStage`; tab URL/domain fields | Not stated | [Build tabs for meetings](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/build-tabs-for-meeting). Stop until lifecycle surfaces and meeting types are stated. |
| Notification to users | Conditional | Activity-feed notification uses `webApplicationInfo`, optional `activities`, and possibly RSC. Meeting-targeted notification uses meeting RSC instead | 1.7 for activity-feed notification manifest updates; 1.12 for current RSC form | [Activity feed notifications](https://learn.microsoft.com/en-us/graph/teams-send-activityfeednotifications), [meeting apps APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis). Stop until notification surface is known. |
| In-meeting notification | Conditional | Targeted notification requires `webApplicationInfo` and RSC such as `OnlineMeetingNotification.Send.Chat` under `authorization.permissions.resourceSpecific` | 1.12 for current RSC form | [Meeting apps APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis). Do not infer application permission from a UI toast alone. |
| Fetch participant roles | Conditional | Bot-based participant lookup requires relevant RSC, including documented `OnlineMeetingParticipant.Read.Chat`; tab-only context access can differ | 1.12 for current RSC form | [Meeting apps APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis). Stop until the exact API and meeting type are known. |
| Plan meeting lifecycle | Required for exposed surfaces | `configurableTabs[].context` selects pre-, in-, and post-meeting surfaces; `scopes` selects private/group or channel/team availability | Not stated | [Meeting lifecycle](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/teams-apps-in-meetings), [build tabs for meetings](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/build-tabs-for-meeting). Lifecycle planning itself is not a property. |
| Live Share | Required | Meeting tab contexts, normally `meetingSidePanel` and `meetingStage`; `validDomains`; delegated RSC names documented as `LiveShareSession.ReadWrite.Chat`, `LiveShareSession.ReadWrite.Group`, `MeetingStage.Write.Chat`, and `ChannelMeetingStage.Write.Group` as applicable | 1.12 for current RSC form | [Live Share core capabilities](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/teams-live-share-capabilities). Add only permissions required by chosen scopes and contexts. |

## Webhooks and connectors

Base source: [Webhooks and connectors](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/what-are-webhooks-and-connectors).

### Outgoing Webhooks

Outgoing Webhooks are configured per team and cannot be included in a normal Teams app package. They have **no Teams manifest effect**.

| Feature-map item | Effect | Verified manifest properties | Primary source and stop notes |
|---|---|---|---|
| Post data to external app | None | None | [Create Outgoing Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-outgoing-webhook). Configuration is made in Teams and secured with HMAC. |
| Request data from external app | None | None | [Create Outgoing Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-outgoing-webhook). The service must respond within the webhook contract; no manifest entry exists. |
| Send cards | None | None | [Create Outgoing Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-outgoing-webhook). Card actions are limited; this is runtime response content. |

### Incoming Webhooks

Incoming Webhooks created directly or with Workflows are channel/workflow configuration, not a normal app-manifest capability.

| Feature-map item | Effect | Verified manifest properties | Primary source and stop notes |
|---|---|---|---|
| Collect user input | None / stop | None | [Incoming Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook). Incoming Webhook Adaptive Cards do not support `Action.Submit`. Use a bot, message extension, or suitable Workflow for interactive input. |
| Refresh cards | None / stop | None | [Universal Actions refresh](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/universal-actions-for-adaptive-cards/work-with-universal-actions-for-adaptive-cards). Refresh requires a bot-backed Universal Action flow; do not infer it for a simple Incoming Webhook. |
| Send cards | None | None | [Incoming Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook). The card is in the POST payload, not the Teams manifest. |

### Connectors for Microsoft 365 Groups

| Feature-map item | Effect | Verified manifest properties | Primary source and stop notes |
|---|---|---|---|
| Collect user input | Stop for new work | Historical `connectors[].connectorId`, `connectors[].configurationUrl`, `connectors[].scopes` | [Create connectors for Microsoft 365 Groups](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-creating). Microsoft states new connector registration is blocked and connectors are nearing deprecation; prefer Workflows or a bot. |
| Customize user experience | Stop for new work | Historical `connectors[]` configuration page fields | [Manage Microsoft 365 connectors](https://learn.microsoft.com/en-us/microsoftteams/m365-custom-connectors). Use only to maintain an existing registered connector after human approval. |
| Send cards | Stop for new work | Historical `connectors[]`; card content is not a manifest field | [Create and send actionable messages](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-using). Teams connectors do not support Adaptive Cards. |
| Refresh cards | Stop | No verified modern manifest setting | [Webhooks and connectors](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/what-are-webhooks-and-connectors) states connectors cannot update messages. Do not generate a refresh configuration from the old feature-map image. |

## Graph conversational interface

Base sources: [Microsoft Graph Teams overview](https://learn.microsoft.com/en-us/graph/teams-concept-overview) and [permissions in Teams apps](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/app-permissions/teams-app-permissions).

| Feature-map item | Effect | Verified manifest properties | Minimum schema | Primary source and stop notes |
|---|---|---|---|---|
| App management | Conditional | Usually none in the Teams manifest; selected/RSC cases can use `authorization.permissions.resourceSpecific` | 1.12 for current RSC form | [teamsApp resource](https://learn.microsoft.com/en-us/graph/api/resources/teamsapp), [Graph permissions reference](https://learn.microsoft.com/en-us/graph/permissions-reference). Stop until the exact endpoint and delegated/application/RSC model are known. |
| Messaging in channel and chat | Conditional | Classic Graph permissions: none in Teams manifest. Scope-specific RSC: `authorization.permissions.resourceSpecific`; examples include `ChannelMessage.Read.Group` and `ChatMessage.Read.Chat` | 1.12 for current RSC form | [Get all channel and chat messages](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/channel-messages-for-bots-and-agents), [message change notifications](https://learn.microsoft.com/en-us/graph/teams-changenotifications-chatmessage). Never grant read-all permissions from generic message code evidence. |
| Send activity feed notification | Required | `webApplicationInfo`; `activities.activityTypes[]` for traditional templated types; optional omission of `activities` for reserved `systemDefault`; optional RSC in `authorization.permissions.resourceSpecific` (`TeamsActivity.Send.User`, `.Group`, `.Chat`) | 1.7 | [Send activity feed notifications](https://learn.microsoft.com/en-us/graph/teams-send-activityfeednotifications). Stop until notification mode, target scope, and permission model are known. |
| Meeting transcript and recording | Conditional | Classic Entra permissions: no Teams manifest permission. App-installed meeting access with RSC uses `authorization.permissions.resourceSpecific`, including `OnlineMeetingTranscript.Read.Chat` and `OnlineMeetingRecording.Read.Chat` where applicable | 1.12 for current RSC form | [Transcript and recording change notifications](https://learn.microsoft.com/en-us/graph/teams-changenotifications-callrecording-and-calltranscript), [fetch transcripts and recordings](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/meeting-transcripts/overview-transcripts). These RSC permissions apply to scheduled private chat meetings, not channel meetings. Tenant controls also apply. |
| Call provisioning and management | Conditional | A calls/meetings bot needs `bots[].supportsCalling` and optionally `supportsVideo`; Graph cloud-communications permissions remain in Entra | Not stated | [Cloud communications API](https://learn.microsoft.com/en-us/graph/cloud-communications-concept-overview), [register a calls and meetings bot](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/calls-and-meetings/registering-calling-bot). Stop if the sample only creates online meetings and has no callable bot. |
| Export and import messages in channel and chat | None | No feature-specific Teams manifest field for classic Graph application permissions | Not stated | [Export Teams content](https://learn.microsoft.com/en-us/microsoftteams/export-teams-content), [import external messages](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/import-messages/import-external-messages-to-teams). These are privileged compliance/migration operations. Require explicit human approval; never infer them from normal messaging code. |

## Cross-cutting stop conditions

Stop manifest generation and report `needs-input` when any of these conditions applies:

- The public HTTPS domain, app ID, bot ID, Entra app ID, or app ID URI is unknown.
- Code evidence does not distinguish `personal`, `groupChat`, and `team` installation scopes.
- A feature needs a choice between classic Entra permission and RSC.
- A requested Graph permission is broader than the code evidence.
- Meeting type, lifecycle surface, or target participant set is unclear.
- Documentation uses preview schema, a schema newer than the repository pin, or a retired feature.
- The feature-map label describes runtime behavior but no official source identifies a manifest property.
- Existing manifest values conflict with detected code. Preserve the manifest and report the conflict; do not silently replace user intent.
