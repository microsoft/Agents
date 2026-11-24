# Teams meetings agent (TypeScript)

Migrated from the [Teams SDK Node sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-meetings/nodejs/bot-meetings)
to Microsoft 365 Agents SDK. Requires Node 22 or newer; synchronization CI uses Node 24.

```sh
npm ci
npm run build
```

Provision a Teams-managed bot in the [Teams Developer Portal](https://dev.teams.microsoft.com),
as described by the [upstream setup](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-meetings#setup-instructions).
Under **Tools → Bot management**, enable the **Participant Join** and **Participant Leave**
meeting event subscriptions. Copy `env.TEMPLATE` to `.env`, fill the bot's Entra
app credentials, run `npm start`, and expose port 3978 over HTTPS. Set the bot's
messaging endpoint to `https://YOUR_HOST/api/messages`. Never commit credentials.

Fill the placeholders in `manifest/manifest.json`, zip it with the two icons,
and install the package in the meeting. Keep `webApplicationInfo.id` equal to
the bot ID and retain the three Application resource-specific permissions for
scheduled/channel meeting events and participant events. These permissions and
subscriptions are required for event delivery; installing a bot alone is insufficient.
See the [Teams meeting event prerequisites](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis#receive-real-time-teams-meeting-events).

The agent handles meeting start/end and participant join/leave events. At meeting end it obtains the Graph resource ID and organizer, waits 30 seconds, then tries transcript retrieval up to three times with 10-second delays. It selects the latest transcript and converts VTT speaker tags to readable text. No transcript produces a clear fallback card.

`src/agent.ts` exports a factory without starting the server. Protected contracts
exercise real SDK routes with an in-memory adapter. External services and delays
are injected in tests; no tenant credentials are needed. After building, run from
the repository root:

```sh
node automation/teams-sample-sync/tests/js-contracts/bot-meetings.test.mjs samples/nodejs/teams/bot-meetings
```

Test meeting event delivery, participant identity/roles and credentialed Graph transcript retrieval. Transcripts must be enabled and available for the meeting. Configure the service connection with application credentials, tenant admin consent for `OnlineMeetingTranscript.Read.All`, and the required application access policy for the organizer. Review the linked Graph documentation before provisioning permissions.

Graph reference: [List meeting transcripts](https://learn.microsoft.com/en-us/graph/api/onlinemeeting-list-transcripts?view=graph-rest-beta).
