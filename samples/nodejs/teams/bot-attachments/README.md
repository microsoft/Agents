# Teams file attachments agent (TypeScript)

Migrated from the [Teams SDK Node sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-attachments/nodejs/bot-attachments)
to Microsoft 365 Agents SDK. Requires Node 22 or newer; synchronization CI uses Node 24.

```sh
npm ci
npm run build
```

Copy `env.TEMPLATE` to `.env` and fill in the app registration credentials. Run
`npm start`, expose port 3978 over HTTPS, and configure the Azure Bot messaging
endpoint as `https://YOUR_HOST/api/messages`. Fill the placeholders in the actual
`manifest/manifest.json`, zip it with `color.png` and `outline.png`, and upload
that package in Teams. Never commit credentials.

Send a file in a personal chat. The agent downloads its content and requests consent to save it to OneDrive. Accept to upload and receive a file-info card, or decline to discard the pending content. Consumed or failed uploads cannot be replayed.

`src/agent.ts` exports a factory without starting the server. Protected contracts
exercise real SDK routes with an in-memory adapter. External services and delays
are injected in tests; no tenant credentials are needed. After building, run from
the repository root:

```sh
node automation/teams-sample-sync/tests/js-contracts/bot-attachments.test.mjs samples/nodejs/teams/bot-attachments
```

Test file download, the consent dialog, OneDrive upload and rendering of the final file-info card in Teams. Pending uploads use in-memory buffers and are lost on restart.

Accepted uploads run in a tracked in-memory queue so the consent invoke can be
acknowledged before the transfer finishes. Completion uses a new proactive turn.
Queued work is lost on restart; a production application should use durable work
storage when delivery must survive restarts. The factory exposes `waitForUploads()`
for tests or an orderly shutdown to wait for outstanding work.
