# Teams cards agent (TypeScript)

Demonstrates nested Adaptive Cards, form submission and visibility controls with
Microsoft 365 Agents SDK. Source: [Teams SDK cards sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-cards/nodejs/bot-cards).

Requires Node 22 or newer; synchronization CI uses Node 24.

```sh
npm ci
npm run build
```

Copy `env.TEMPLATE` to `.env` and fill in the app registration values. Run
`npm start`, expose port 3978 over HTTPS, and configure the Azure Bot messaging
endpoint as `https://YOUR_HOST/api/messages`. Fill the placeholders in
`manifest/manifest.json`, then zip the manifest and its two icons to upload in
Teams. Keep credentials out of the manifest and source control.

Send `card actions` to see nested cards and submit a name, or `toggle visibility`
to show/hide content. Matching is case-insensitive; the card-actions branch takes
precedence when a message contains both commands. Unknown text displays help.
The submitted name produces a message and an Adaptive Card invoke response.

The module `src/agent.ts` exports a factory without opening a server so protected
contracts can run real SDK routing with an in-memory adapter. From the repository
root, after building this sample:

```sh
node automation/teams-sample-sync/tests/js-contracts/bot-cards.test.mjs samples/nodejs/teams/bot-cards
```

Local contracts do not verify credentialed Teams/Entra/Azure behavior. Test card
rendering, form submission, the manifest upload and authentication in a Teams tenant.
