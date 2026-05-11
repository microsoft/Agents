# Copilot SDK — Dungeon Scribe Agent

This sample shows how to embed the GitHub Copilot SDK inside a Microsoft 365 Agent. The agent adopts a **Dungeon Scribe** persona, responds as a fantasy RPG note-keeper, and uses Copilot SDK tools to roll dice and manage an adventurer's inventory.

> [!NOTE]
> The GitHub Copilot SDK is currently in Public Preview. APIs, package versions, and authentication flows may change before general availability.

## Overview

The sample combines the Microsoft 365 Agents SDK hosting model with the GitHub Copilot SDK session API:

- `@microsoft/agents-hosting-express` hosts the Microsoft 365 Agent endpoint.
- `@github/copilot-sdk` powers the Dungeon Scribe's reasoning and tool calling.
- `zod` schemas define strongly-typed tool parameters for dice rolling and inventory management.
When a message arrives, the sample creates a Copilot SDK session, injects the Dungeon Scribe system persona, and lets the model call RPG-specific tools as needed.

## Prerequisites

- [Node.js](https://nodejs.org) version 20.6 or higher
- A Microsoft 365 Agent or Azure Bot configuration that can send activities to this sample
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for local WebChat testing
- [GitHub Copilot CLI](https://www.npmjs.com/package/@github/copilot) installed globally
- GitHub Copilot CLI authenticated locally

```bash
node --version
copilot --version
copilot auth login
```

## Setup

1. Open `samples\nodejs\copilot-sdk` in your terminal.
2. Install dependencies:

   ```bash
   npm install
   ```

3. Create a `.env` file from the template and fill in your Microsoft Entra app registration details:

   ```bash
   copy env.TEMPLATE .env
   ```

   Required settings:
   - `connections__serviceConnection__settings__clientId`
   - `connections__serviceConnection__settings__clientSecret`
   - `connections__serviceConnection__settings__tenantId`

4. (Optional) Set `COPILOT_MODEL` in `.env` if you want to override the default `gpt-4.1` model.
5. Start a development tunnel so WebChat or the Azure Bot channel can reach your local agent:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

6. Copy the tunnel URL shown after `Connect via browser:` and use `{tunnel-url}/api/messages` as the messaging endpoint for your bot or agent registration.

## Run the sample

Start the Dungeon Scribe agent:

```bash
npm start
```

After startup, the console should show the agent listening on port `3978`.

## Test the sample

### WebChat / Azure Bot Service

1. Configure your bot or Microsoft 365 Agent to use the dev tunnel endpoint.
2. Open **Test in WebChat** (or your preferred chat surface).
3. Start a conversation and try prompts such as:
   - `Roll 2d20+5 for initiative`
   - `Add Rope of Climbing to inventory`
   - `List my inventory`
   - `Describe the ruined keep at dusk`

### Agents Playground

This sample includes the same test tooling used by other Node.js samples in this repo:

```bash
npm run test-tool
```

Or run the local anonymous host and playground together:

```bash
npm test
```

## What this sample demonstrates

- Embedding the GitHub Copilot SDK in a Microsoft 365 Agent
- Defining custom Copilot tools with Zod schemas
- Managing per-conversation inventory state for tool calls
- Creating Copilot sessions on demand for each incoming activity
- A foundation for adding streaming handlers with `session.on(...)` events if you want token-by-token output later

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [GitHub Copilot](https://github.com/features/copilot)