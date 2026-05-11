# Copilot SDK — Dungeon Scribe Agent

This sample embeds the GitHub Copilot SDK inside a Microsoft 365 Agent to create **Dungeon Scribe**, a fantasy RPG note-keeper that can narrate adventures, roll dice, and track inventory.

> **Public Preview:** The GitHub Copilot SDK is currently in **Public Preview** and may change before general availability.

This sample follows the Microsoft 365 Agents Python hosting pattern and shows how to connect an M365 Agent to a Copilot-powered assistant with custom tools.

## Prerequisites

- [Python](https://www.python.org/) 3.10 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for local development
- GitHub Copilot CLI installed globally
- GitHub Copilot CLI authenticated locally
- An Azure Bot / Microsoft 365 Agent registration with client ID, tenant ID, and client secret

Install and authenticate the Copilot CLI if needed:

```bash
npm install -g @github/copilot
copilot auth login
```

## Local Setup

### Configuration

1. Open the `env.TEMPLATE` file in the root of this sample, rename it to `.env`, and configure the following values:
   1. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID` to the App ID of your bot or agent identity.
   2. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET` to the client secret for that identity.
   3. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID` to the Microsoft Entra tenant ID where the app is registered.
   4. (Optional) Set `COPILOT_MODEL` to override the default Copilot model (`gpt-4.1`).

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

### Agents Playground

You can also test the sample in the Microsoft 365 Agents Playground or other compatible local testing experiences by pointing them at the same tunneled `/api/messages` endpoint.

## What This Sample Demonstrates

- **GitHub Copilot SDK in an M365 Agent:** wires a `CopilotClient` into the Microsoft Agents hosting loop.
- **Custom tools:** exposes `roll_dice` and `manage_inventory` with `@define_tool` and Pydantic parameter models.
- **Session management concepts:** starts the Copilot client once and creates a session per incoming turn for simplicity; production apps can resume sessions for richer multi-turn memory.
- **Streaming concepts:** uses the Copilot SDK request/response flow that can be extended to streaming scenarios as the SDK evolves.
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

- The inventory tool keeps state in memory and isolates data by conversation ID.
- This sample approves all Copilot permission requests for local development simplicity.
- If Copilot requests fail, verify that the Copilot CLI is installed and authenticated in the same environment where the agent is running.

To learn more about building Bots and Agents, see the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
