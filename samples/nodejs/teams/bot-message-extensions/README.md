# Teams message extensions agent (TypeScript)

Search Wikipedia from the Teams compose area and unfurl Wikipedia links with
Microsoft 365 Agents SDK. Migrated from the [Teams SDK Node sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/bot-message-extensions/nodejs/bot-message-extensions).

Requires Node 22 or newer; synchronization CI uses Node 24.

```sh
npm ci
npm run build
```

Copy `env.TEMPLATE` to `.env` and fill in the app registration credentials. Run
`npm start`, expose port 3978 over HTTPS, and configure the Azure Bot messaging
endpoint as `https://YOUR_HOST/api/messages`. Fill the placeholders in the actual
`manifest/manifest.json`, zip it with `color.png` and `outline.png`, and upload
the package in Teams.

The manifest declares the `wikipediaSearch` query command in
`composeExtensions[0].commands`. Search results contain Adaptive Cards with
thumbnail previews and links to Wikipedia. Empty searches return a message.
The link handler supports `en.wikipedia.org`. Send `help` for usage or another
message to receive an echo.

The application factory accepts an optional `searchWikipedia` function so local
contracts can exercise real SDK routing without depending on the external site.
After building, run the protected contracts from the repository root:

```sh
node automation/teams-sample-sync/tests/js-contracts/bot-message-extensions.test.mjs samples/nodejs/teams/bot-message-extensions
```

The local tests do not verify credentialed Teams authentication or deployment.
Manually check package upload, compose search, thumbnail rendering and link
unfurling in your Teams tenant. Search also requires outbound Wikipedia access.
