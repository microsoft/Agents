# Copilot SDK — Dungeon Scribe Agent

This sample embeds the GitHub Copilot SDK inside a Microsoft 365 Agent with **per-user GitHub OAuth authentication** to create **Dungeon Scribe**, a fantasy RPG note-keeper that can narrate adventures, roll dice, and track inventory — all running under each user's own GitHub Copilot identity.

> [!NOTE]
> The GitHub Copilot SDK is currently in Public Preview. APIs, package versions, and authentication flows may change before general availability.

## Overview

The sample combines the Microsoft 365 Agents SDK hosting model with the GitHub Copilot SDK session API:

- `@microsoft/agents-hosting-express` hosts the Microsoft 365 Agent endpoint.
- `@github/copilot-sdk` powers the Dungeon Scribe's reasoning and tool calling.
- `zod` schemas define strongly-typed tool parameters for dice rolling and inventory management.

When a message arrives, the sample authenticates the user via GitHub OAuth, creates a Copilot SDK session with the user's token, injects the Dungeon Scribe system persona, and lets the model call RPG-specific tools as needed.

## Prerequisites

- [Node.js](https://nodejs.org) version 20.6 or higher
- A Microsoft 365 Agent or Azure Bot configuration that can send activities to this sample
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for local WebChat testing
- [GitHub Copilot CLI](https://www.npmjs.com/package/@github/copilot) installed globally
- Each user must have a [GitHub Copilot subscription](https://github.com/features/copilot)

```bash
node --version
```

## Setup

### 1. Create a GitHub OAuth App

1. Go to [GitHub Developer Settings → OAuth Apps](https://github.com/settings/developers)
2. Click **New OAuth App**
3. Set **Authorization callback URL** to: `https://token.botframework.com/.auth/web/redirect`
4. Enable **Device Flow**
5. Note the **Client ID** and **Client Secret**

### 2. Create an Azure Bot OAuth Connection

In the Azure Portal, navigate to your Azure Bot resource:

1. Go to **Settings → Configuration → OAuth Connection Settings → Add Setting**
2. Set **Service Provider** to **GitHub** (native option)
3. Enter the **Client ID** and **Client Secret** from your GitHub OAuth App
4. Set **Scopes** to `user repo`
5. Note the **Connection Name** (e.g., `github-oauth`)

### 3. Configure the Sample

1. Open `samples\nodejs\copilot-sdk` in your terminal.
2. Install dependencies:

   ```bash
   npm install
   ```

3. Create a `.env` file from the template and fill in your settings:

   ```bash
   copy env.TEMPLATE .env
   ```

   Required settings:
   - `connections__serviceConnection__settings__clientId` — App ID of your bot registration
   - `connections__serviceConnection__settings__clientSecret` — Client secret
   - `connections__serviceConnection__settings__tenantId` — Microsoft Entra tenant ID
   - `AgentApplication__UserAuthorization__Handlers__github__Settings__azureBotOAuthConnectionName` — The OAuth connection name created in **Create an Azure Bot OAuth Connection** above (e.g., `github-oauth`)

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

Commands:

- `-signout` — Sign out of your GitHub account and reset the OAuth flow

### Agents Playground

This sample includes the same test tooling used by other Node.js samples in this repo:

```bash
npm run test-tool
```

Or run the local anonymous host and playground together:

```bash
npm test
```

## How Authentication Works

1. When a user sends their first message, the Agents SDK `UserAuthorization` system triggers the GitHub OAuth flow via the configured Azure Bot OAuth Connection.
2. The user signs in to GitHub and authorizes the app. Azure Bot Service returns a `gho_` OAuth token.
3. The agent retrieves the token via `agentApp.authorization.getToken(context, 'github')` and passes it to `createSession({ gitHubToken })`.
4. Each Copilot session runs under the authenticated user's GitHub identity, enabling per-user model access, quota tracking, and content exclusion.

## What this sample demonstrates

- **GitHub Copilot SDK in an M365 Agent:** embeds a `CopilotClient` inside the Microsoft Agents hosting loop
- **Per-user GitHub OAuth:** authenticates each user via Azure Bot Service GitHub OAuth and passes their token to the Copilot SDK
- **Custom tools with Zod schemas:** `roll_dice` and `manage_inventory` with strongly-typed parameters
- **Per-user session management:** sessions keyed by `userId:conversationId` for multi-turn context
- **Streaming:** token-by-token response delivery using Copilot SDK event callbacks
- **Fantasy persona orchestration:** system prompt for dramatic dungeon chronicler behavior

## Project Structure

```text
src/
  index.ts                # Agent + Copilot SDK integration + Express server
  tools/
    diceRoller.ts         # Dice rolling tool (Zod schema)
    inventoryManager.ts   # In-memory inventory tool (Zod schema)
```

## Notes

- The inventory tool keeps state in memory and isolates data by session key (user + conversation).
- This sample approves all Copilot permission requests for local development simplicity.
- If Copilot requests fail, verify that the signed-in GitHub account has an active Copilot subscription and try `-signout` to restart the OAuth flow.
- Each user whose GitHub token is used must have a GitHub Copilot subscription.

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [GitHub Copilot](https://github.com/features/copilot)
- [Azure Bot OAuth Connection setup](https://aka.ms/AgentsSDK-AddAuth)
