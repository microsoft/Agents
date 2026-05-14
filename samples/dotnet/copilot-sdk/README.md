# Copilot SDK — Dungeon Scribe Agent

This sample shows how to embed the GitHub Copilot SDK inside a Microsoft 365 Agent with **per-user GitHub OAuth authentication**. The result is **Dungeon Scribe**, a fantasy RPG note-keeper that can narrate with flair, roll dice through Copilot tools, and maintain a simple per-conversation inventory — all running under each user's own GitHub Copilot identity.

> [!NOTE]
> The GitHub Copilot SDK for .NET is currently in public preview and may change before general availability.

## Overview

Dungeon Scribe hosts an `AgentApplication` in ASP.NET Core, then uses the GitHub Copilot SDK at message time to:

- authenticate each user via GitHub OAuth (through Azure Bot Service)
- create a Copilot session with the user's GitHub token for per-user identity
- create a Copilot session with a custom fantasy persona
- expose tool functions for dice rolling and inventory management
- stream the model response token-by-token back to the chat client

## Prerequisites

Before you run the sample, install and configure the following:

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download/dotnet/8.0)
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)
- A Microsoft 365 Agent or Azure Bot registration for the hosting endpoint
- GitHub Copilot CLI installed and authenticated
  - `npm install -g @github/copilot`
- Each user must have a [GitHub Copilot subscription](https://github.com/features/copilot)

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

### 3. Configure appsettings

Copy the template to a local development settings file and replace the `{{...}}` placeholders:

```powershell
Copy-Item appsettings.TEMPLATE.json appsettings.Development.json
```

`appsettings.Development.json` is ignored by git, so it is safe for local secrets. Configure these values:

- `{{ClientId}}` — the App ID of your bot registration
- `{{ClientSecret}}` — the client secret for the bot registration
- `{{TenantId}}` — the Microsoft Entra tenant ID where the app is registered
- `{{GitHubOAuthConnectionName}}` — the OAuth connection name created in **Create an Azure Bot OAuth Connection** above

For local development, token validation is disabled by default. Enable it when you are ready to test with authenticated channels.

### 4. Start a dev tunnel

Host a tunnel for the local ASP.NET app:

```bash
devtunnel host -p 3978 --allow-anonymous
```

Update your bot or agent messaging endpoint to:

```text
https://<your-tunnel-host>/api/messages
```

## Run the sample

From this directory, start the agent host:

```bash
dotnet run
```

You can optionally set a different Copilot model before starting the app:

```powershell
$env:COPILOT_MODEL = "gpt-4.1"
dotnet run
```

## Testing

You can test the sample with either of the following:

- **Agents Playground** for local development and message inspection
- **Azure Bot WebChat** via **Test in WebChat**
- **Teams / Microsoft 365** after packaging and uploading a Teams app manifest for your bot

Example prompts:

- `roll 2d6+3`
- `add Rope of Climbing to inventory`
- `list inventory`
- `describe the ruins beneath the moonlit keep`

Commands:

- `-signout` — Sign out of your GitHub account and reset the OAuth flow

## How authentication works

1. When a user sends their first message, the Agents SDK `UserAuthorization` system triggers the GitHub OAuth flow via the configured Azure Bot OAuth Connection.
2. The user signs in to GitHub and authorizes the app. Azure Bot Service returns a `gho_` OAuth token.
3. The agent retrieves the token via `UserAuthorization.GetTurnTokenAsync()` and passes it to the Copilot SDK's `SessionConfig.GitHubToken`.
4. Each Copilot session runs under the authenticated user's GitHub identity, enabling per-user model access, quota tracking, and content exclusion.

## What this sample demonstrates

This sample demonstrates how to:

- host a Microsoft 365 Agent with ASP.NET Core
- embed the GitHub Copilot SDK in an agent message handler
- authenticate users with GitHub OAuth via Azure Bot Service
- pass per-user GitHub tokens to the Copilot SDK for identity isolation
- define custom Copilot tools for structured gameplay actions
- stream responses token-by-token to chat clients
- keep lightweight conversation-scoped state for inventory tracking

## Further reading

- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [GitHub Copilot SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK/)
- [Azure Bot OAuth Connection setup](https://aka.ms/AgentsSDK-AddAuth)
