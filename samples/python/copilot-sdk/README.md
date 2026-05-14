# Copilot SDK — Dungeon Scribe Agent

This sample embeds the GitHub Copilot SDK inside a Microsoft 365 Agent with **per-user GitHub OAuth authentication** to create **Dungeon Scribe**, a fantasy RPG note-keeper that can narrate adventures, roll dice, and track inventory — all running under each user's own GitHub Copilot identity.

> **Public Preview:** The GitHub Copilot SDK is currently in **Public Preview** and may change before general availability.

This sample follows the Microsoft 365 Agents Python hosting pattern and shows how to connect an M365 Agent to a Copilot-powered assistant with custom tools and per-user authentication.

## Prerequisites

- [Python](https://www.python.org/) 3.10 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for local development
- GitHub Copilot CLI installed globally
- An Azure Bot / Microsoft 365 Agent registration with client ID, tenant ID, and client secret
- Each user must have a [GitHub Copilot subscription](https://github.com/features/copilot)
Install GitHub Copilot CLI if needed:

```bash
npm install -g @github/copilot
```

## Local Setup

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

### 3. Configure and Install

1. Open the `env.TEMPLATE` file in the root of this sample, rename it to `.env`, and configure the following values:
   1. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID` to the App ID of your bot or agent identity.
   2. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET` to the client secret for that identity.
   3. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID` to the Microsoft Entra tenant ID where the app is registered.
   4. Set `AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__GITHUB__SETTINGS__AZUREBOTOAUTHCONNECTIONNAME` to the OAuth connection name created in **Create an Azure Bot OAuth Connection** above (e.g., `github-oauth`).
   5. (Optional) Set `COPILOT_MODEL` to override the default Copilot model (`gpt-4.1`).

2. (Optional but recommended) Create and activate a virtual environment.

3. Install dependencies:

```bash
pip install -r requirements.txt
```

4. Start a dev tunnel and allow anonymous access:

```bash
devtunnel host -p 3978 --allow-anonymous
```

5. Copy the URL shown after `Connect via browser:` and configure your bot or agent messaging endpoint as `{tunnel-url}/api/messages`.

## Running the Agent

Start the application from this sample directory:

```bash
python -m src.main
```

When the service starts successfully, you should see output similar to:

```text
======== Running on http://localhost:3978 ========
```

## Testing the Agent

### WebChat

1. Open your Azure Bot resource.
2. Select **Test in Web Chat**.
3. Try prompts such as:
   - `roll 2d20+5`
   - `add Potion of Healing to inventory`
   - `list inventory`
   - `describe a ruined keep on the edge of a haunted marsh`

Commands:

- `-signout` — Sign out of your GitHub account and reset the OAuth flow

### Agents Playground

You can also test the sample in the Microsoft 365 Agents Playground or other compatible local testing experiences by pointing them at the same tunneled `/api/messages` endpoint.

## How Authentication Works

1. When a user sends their first message, the Agents SDK `UserAuthorization` system triggers the GitHub OAuth flow via the configured Azure Bot OAuth Connection.
2. The user signs in to GitHub and authorizes the app. Azure Bot Service returns a `gho_` OAuth token.
3. The agent retrieves the token via `AGENT_APP.auth.get_token(context, "GITHUB")` and passes it to the Copilot SDK's `create_session(github_token=...)`.
4. Each Copilot session runs under the authenticated user's GitHub identity, enabling per-user model access, quota tracking, and content exclusion.

## What This Sample Demonstrates

- **GitHub Copilot SDK in an M365 Agent:** wires a `CopilotClient` into the Microsoft Agents hosting loop.
- **Per-user GitHub OAuth:** authenticates each user via Azure Bot Service GitHub OAuth and passes their token to the Copilot SDK.
- **Custom tools:** exposes `roll_dice` and `manage_inventory` with `@define_tool` and Pydantic parameter models.
- **Session management:** starts the Copilot client once and creates sessions keyed per user+conversation for multi-turn context.
- **Streaming:** streams token-by-token responses using Copilot SDK event callbacks.
- **Fantasy persona orchestration:** applies a system prompt so the assistant behaves like a dramatic dungeon chronicler.

## Project Structure

```text
src/
  agent.py          # Microsoft 365 Agent + Copilot SDK integration
  main.py           # Logging setup and local server startup
  start_server.py   # aiohttp hosting entry point
  tools/
    dice.py         # Dice rolling tool
    inventory.py    # In-memory inventory tool
```

## Notes

- The inventory tool keeps state in memory and isolates data by session key (user + conversation).
- This sample approves all Copilot permission requests for local development simplicity.
- If Copilot requests fail, verify that the signed-in GitHub account has an active Copilot subscription and try `-signout` to restart the OAuth flow.
- Each user whose GitHub token is used must have a GitHub Copilot subscription.

## Further Reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [GitHub Copilot SDK](https://pypi.org/project/github-copilot-sdk/)
- [Azure Bot OAuth Connection setup](https://aka.ms/AgentsSDK-AddAuth)
