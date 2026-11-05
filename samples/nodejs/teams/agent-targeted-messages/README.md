# Targeted reminder agent (TypeScript)

Migrated from the [Teams SDK Node sample](https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/b9aedfea98e3505acbcf68a3931be05a780d6149/samples/TeamsSDK/agent-targeted-messages/nodejs/agent-targeted-messages)
to Microsoft 365 Agents SDK. Requires Node 22 or newer; synchronization CI uses Node 24.

```sh
npm ci
npm run build
```

Copy `env.TEMPLATE` to `.env`, supply app registration credentials, run `npm start`,
and expose port 3978 over HTTPS. Configure the bot messaging endpoint as
`https://YOUR_HOST/api/messages`. Fill the placeholders in `manifest/manifest.json`,
zip it with `color.png` and `outline.png`, and install the package in Teams.
Never commit credentials.

Use `remind me in 30 seconds to check email` or mention another user with
`remind @John in 5 minutes review PR`. Seconds, minutes and hours support their
usual abbreviated forms. Confirmation, help and reminder lists target the caller;
delayed deliveries target the chosen recipient through a saved conversation
reference and the SDK proactive API. The conversation ID's message suffix is
removed for continuation. Use `my-reminders`, `cancel-reminder [id]`, or the
cancel/dismiss/snooze card actions. Only a reminder's creator or recipient can cancel it.

The app manifest declares `remind` for mention, `my-reminders` for slash, and
`reminder-help` for mention and slash. Keep `supportsTargetedMessages` enabled.
Slash replies preserve `targetedMessageInfo` for prompt preview. Group replies
use the SDK's targeted activity treatment and explicit recipient.

`add-reaction like` and `remove-reaction heart` apply to a quoted message,
`replyToId`, or the current message in that order. These commands require ordinary
@mention messages; slash commands return a private explanation. Added reaction
events are acknowledged. Suggested-action chips invoke the same reminder/help/list
commands. Unknown text returns guidance and the upstream AI-generated label;
there is no LLM integration.

Reminders and timers are in memory and disappear on restart. This sample does
not provide durable or distributed scheduling. Delays must be positive and at
most Node's timer limit (about 24 days); longer delays would otherwise overflow.
Test channel/group privacy, slash prompt preview, proactive authentication,
reaction permissions, and card actions in a real tenant before deployment.

`src/agent.ts` exports a factory without starting the server. Protected contracts
use real SDK routing, a memory transport and an injected clock/scheduler. Run after building:

```sh
node automation/teams-sample-sync/tests/js-contracts/agent-targeted-messages.test.mjs samples/nodejs/teams/agent-targeted-messages
```
