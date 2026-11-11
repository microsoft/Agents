# Meetings and calls

Primary sources:

- [Apps for Teams meetings](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/teams-apps-in-meetings)
- [Meeting apps APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis)
- [Resource-specific consent](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/rsc/grant-resource-specific-consent)

Meeting features combine manifest declarations with configuration outside the manifest. Report these separately.

## Evidence and decisions

| Source evidence | Possible manifest impact | External setup |
|---|---|---|
| Meeting start/end routes | `webApplicationInfo` and meeting RSC permissions for supported meeting types | Enable supported meeting event subscription when required |
| Participant join/leave routes | `OnlineMeetingParticipant.Read.Chat` RSC | Select participant join/leave subscriptions in Developer Portal |
| Get meeting details | Private or channel meeting RSC permission | Bot registration and installation in meeting context |
| Targeted meeting notification | `OnlineMeetingNotification.Send.Chat` RSC | Runtime recipient and meeting data |
| Meeting tab surfaces | Static/configurable tab contexts and domains | Hosted tab application |
| Transcript through Microsoft Graph | Depends on delegated, app-only, or RSC access model | Microsoft Entra Graph permission and consent can be required |
| Calling/video bot | `supportsCalling` or `supportsVideo` when verified | Calling configuration and registration outside manifest |

## RSC examples

For manifest version 1.12 and later, verified RSC permissions use:

```json
{
  "webApplicationInfo": {
    "id": "${{ENTRA_APP_ID}}",
    "resource": "<verified-resource-uri>"
  },
  "authorization": {
    "permissions": {
      "resourceSpecific": [
        {
          "name": "<verified-permission-name>",
          "type": "Application"
        }
      ]
    }
  }
}
```

Select permission names from the exact API and meeting type. Common examples in official meeting documentation include:

- `OnlineMeeting.ReadBasic.Chat`
- `ChannelMeeting.ReadBasic.Group`
- `OnlineMeetingParticipant.Read.Chat`
- `OnlineMeetingNotification.Send.Chat`

These examples are not a default permission bundle. Add only permissions proven by the code and intended meeting scope.

## Required stops

- Meeting type is unclear: private, channel, scheduled, instant, or calling.
- Code uses Graph but the access model is unclear: delegated, application, or RSC.
- `webApplicationInfo.id` is unknown.
- Documentation lists a permission not present in the selected released schema.

Never treat a manifest RSC declaration as Microsoft Entra admin consent. Never place Microsoft Entra-only Graph permissions into `authorization.permissions.resourceSpecific`.

For SSO, `webApplicationInfo.resource` is the real application ID URI and must be known. For RSC-only use, Microsoft documents that `resource` has no RSC operation but must be nonempty. Use a stable repository-approved marker such as the official example `https://RscPermission`, and record that it is RSC-only. Do not present that marker as an SSO resource URI.

Developer Portal event subscriptions and other external setup do not block generation of a verified manifest fragment. They block functional-complete status and must appear in the external-configuration report.

Released schema validates the RSC object shape but can allow arbitrary permission-name strings. Verify each permission name and type against current official feature or RSC documentation; use Developer Portal or platform validation when available.
