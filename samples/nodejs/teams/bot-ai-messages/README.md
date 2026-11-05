# Teams AI messages agent (TypeScript)

Migrated from the [Teams SDK Node sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-ai-messages/nodejs/bot-ai-messages)
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

Send `label`, `sensitivity`, `feedback`, or `citation`. The first matching route wins: a message containing `label` takes precedence over later keywords. Feedback submissions return the selected reaction and entered text.

`src/agent.ts` exports a factory without starting the server. Protected contracts
exercise real SDK routes with an in-memory adapter. External services and delays
are injected in tests; no tenant credentials are needed. After building, run from
the repository root:

```sh
node automation/teams-sample-sync/tests/js-contracts/bot-ai-messages.test.mjs samples/nodejs/teams/bot-ai-messages
```

Test generated-content labels, sensitivity labels, citation rendering and feedback submission in a Teams tenant.
