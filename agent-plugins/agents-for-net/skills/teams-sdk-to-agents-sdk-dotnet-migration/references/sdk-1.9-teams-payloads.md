# Teams payloads with Agents SDK 1.9

Verified against `Microsoft.Agents.Extensions.MSTeams` 1.9.205 and its transitive
`Microsoft.Teams.Apps` 2.1.2 dependency. For a different selected release, inspect
that release's XML documentation and handler delegates before applying these mappings.

Do not add `Microsoft.Teams.Api` to make old code compile. Do not replace every
`Microsoft.Teams.Api` prefix with `Microsoft.Agents.Extensions.MSTeams`: that namespace
contains routing APIs, not the replacement payload models. Keep the three selected
Agents package references; the compatible Teams types arrive transitively.

## Route signatures

Attribute handlers begin with `ITeamsTurnContext context, ITurnState state`, then the
payload below, and end with `CancellationToken cancellationToken`. Preserve route
attributes, keys, ranks, command IDs, and behavior.

| Route | Payload | Return type |
|---|---|---|
| `TeamsFileConsentAcceptRoute` / `TeamsFileConsentDeclineRoute` | `Microsoft.Teams.Apps.Files.FileConsentValue` | `Task` |
| `TeamsTaskFetchRoute` / `TeamsTaskSubmitRoute` | `Microsoft.Teams.Apps.TaskModules.TaskModuleRequest` | `Task<Microsoft.Teams.Apps.TaskModules.TaskModuleResponse>` |
| `TeamsQueryRoute` | `Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQuery` | `Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse>` |
| `TeamsQueryLinkRoute` | `Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQueryLink` | `Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse>` |
| `TeamsMeetingStartRoute` / `TeamsMeetingEndRoute` | `Microsoft.Teams.Apps.Clients.MeetingDetails` | `Task` |
| `TeamsMeetingParticipantsJoinRoute` | `Microsoft.Teams.Apps.Meetings.MeetingParticipantJoinValue` | `Task` |
| `TeamsMeetingParticipantsLeaveRoute` | `Microsoft.Teams.Apps.Meetings.MeetingParticipantLeaveValue` | `Task` |

The Agents meeting-start/end delegate uses `Clients.MeetingDetails`, even though
Teams Apps also declares `MeetingStartValue` and `MeetingEndValue`. Those similarly
named types do not match the Agents attribute delegate.

## File consent

`FileConsentCardResponse` becomes `Files.FileConsentValue`. Its `Context` remains
the application payload, while `UploadInfo` is `Files.FileUploadInfo`.
`UploadUrl` and `ContentUrl` are now `Uri` values; validate null and convert to
strings only when crossing into a string-based upload queue.

For an outbound consent card, preserve the wire fields `description`, `sizeInBytes`,
`acceptContext`, and `declineContext` in an object carried by an Agents `Attachment`
with content type `application/vnd.microsoft.teams.card.file.consent`.
Do not remove consent handlers or replace them with an unvalidated generic invoke
handler to bypass signature errors. Test both accept and decline through dispatch.

## Task modules

`Request` becomes `TaskModuleRequest`. `GetDataString` remains available through
`Microsoft.Agents.Extensions.MSTeams` extensions. Responses use
`TaskModuleResponse.Task`, whose type is `Microsoft.Teams.Apps.TaskModules.Response`.
`Response.Type` is `TaskModuleResponseTypes.Continue` or `.Message`; `Value` contains
the dialog object or completion message.

```csharp
using Microsoft.Teams.Apps.TaskModules;
using Microsoft.Teams.Apps.Schema;

TaskModuleResponse response = TaskModuleResponse.CreateBuilder()
    .WithType(TaskModuleResponseTypes.Continue)
    .WithTitle("Form")
    .WithWidth(400)
    .WithHeight(300)
    .WithCard(new TeamsAttachment
    {
        ContentType = AttachmentContentTypes.AdaptiveCard,
        Content = card
    })
    .Build().Body!;
```

The builder's `Build()` returns an `InvokeResponse<TaskModuleResponse>`; attribute
handlers return its **Body**, because the Agents route writes the invoke envelope.
For URL dialogs, construct `Response.Value` with the original `title`, `width`,
`height`, `url`, and `fallbackUrl` fields. Old `ContinueTask`, `MessageTask`,
`TaskInfo`, and `Size` models are not required. Test fetch, chained submit, and
completion responses, including their JSON nesting.

## Message extensions

Replace `Query`, `Response`, and `Result` with `MessageExtensionQuery`,
`MessageExtensionResponse`, and `ComposeExtension` in `Microsoft.Teams.Apps.MessageExtensions`.
Use `MessageExtensionResponseTypes.Result` / `.Message` and
`Microsoft.Teams.Apps.Schema.TeamsAttachmentLayouts.List`.

Attachments use `Schema.TeamsAttachment` with `AttachmentContentTypes.AdaptiveCard`
or `.ThumbnailCard`, not plain string content types. A search result's `preview`
goes in the attachment's `Properties` extension-data dictionary. Preserve both
the full card and the preview. Link-query URLs are `Uri`; use `ToString()` at a
string-based rendering boundary. Test a populated query, an empty result, and
link unfurling—not just whether the project compiles.

## Other compatibility changes

- `SendTargetedActivityAsync` takes an explicit recipient. Pass the intended
  `ChannelAccount`; setting `activity.Recipient` alone no longer matches the API.
- Use `Client.Conversations.AddReactionAsync` / `DeleteReactionAsync` with a
  reaction string and named `cancellationToken:`. The old `Reactions` facade is obsolete.
- `Clients.MeetingDetails` omits event timestamps and `msGraphResourceId`.
  Preserve timestamps from the raw activity payload. If transcript retrieval needs
  organizer AAD ID and Graph meeting ID, preserve them from the meeting REST response
  using the current turn's authenticated connector transport. Do not silently drop
  transcript lookup or substitute the Teams meeting ID for the Graph ID.
- TestAdapter does not supply the connector/user-token REST transports needed by
  the 1.9 Teams extension. Use a test host that installs both, with mocked HTTP.

## Verification

The repository's `samples/dotnet/teams` implementations and
`automation/teams-sample-sync/tests/contracts` exercise these mappings. Build with
warnings as errors and run the contracts against the same selected SDK line.
When tools rather than shell access are available, use `validate_sample` with
`group: "all"` and fix its concrete errors before claiming completion.

Installed package evidence: `Microsoft.Agents.Extensions.MSTeams.xml` documents
the attribute delegate signatures; `Microsoft.Teams.Apps.xml` documents payloads
and response builders. Consult those files or the corresponding release source
in [Agents-for-net](https://github.com/microsoft/Agents-for-net) and
[teams.net](https://github.com/microsoft/teams.net) when a type remains unresolved.
