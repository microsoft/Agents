# Agent Plugins

This directory contains AI coding assistant plugins for the Microsoft Agents SDK.

Plugins provide skills — contextual guidance that activates automatically when you work on relevant code. When you import `@microsoft/agents-hosting`, your assistant gets Agents SDK knowledge loaded into context.

## Available Plugins

### `agents-sdk-common`

Shared skills that apply to all languages (dotnet, Node.js, Python).

| Skill | Activates when... |
|-------|-------------------|
| `agents-sdk-provision` | Provisioning Azure Bot resources, configuring identity credentials, or setting up OAuth via `az` CLI |

---

### `agents-for-js`

Skills for building agents with the Microsoft 365 Agents SDK for TypeScript/JavaScript.

| Skill | Activates when... |
|-------|-------------------|
| `agents-sdk-typescript` | Code imports `@microsoft/agents-hosting`, `@microsoft/agents-hosting-express`, or related packages, or when building a new agent |
| `agents-sdk-typescript-debugging` | Troubleshooting a Microsoft Agents SDK agent in TypeScript/JavaScript (build errors, auth failures, startup crashes, configuration issues) |

---

### `agents-for-net`

Skills for building agents with the Microsoft 365 Agents SDK for C# / .NET.

| Skill | Activates when... |
|-------|-------------------|
| `agents-sdk-dotnet` | Code imports `Microsoft.Agents.Hosting.AspNetCore`, `Microsoft.Agents.Builder`, or related packages, or when building a new agent in C# / .NET |
| `agents-sdk-dotnet-debugging` | Troubleshooting a Microsoft Agents SDK agent in C# / .NET (build errors, auth failures, startup crashes, configuration issues) |
| `bf-to-agents-sdk-dotnet-migration` | Migrating a Bot Framework .NET SDK bot (`Microsoft.Bot.Builder`) to Microsoft Agents SDK |
| `agents-sdk-dotnet-activityhandler-migration` | Migrating an Agents SDK bot from `ActivityHandler`/`TeamsActivityHandler` to `AgentApplication` routing |

---

## Installing the Plugin Marketplace

The plugin marketplace is hosted at the root of this repository (`.claude-plugin/marketplace.json`).

### Claude Code

Run these commands inside Claude Code:

```
/plugin marketplace add microsoft/Agents
```

Then install the plugins you need:

```
/plugin install agents-sdk-common@microsoft-agents-sdk
/plugin install agents-for-js@microsoft-agents-sdk
/plugin install agents-for-net@microsoft-agents-sdk
```

Skills activate automatically based on what you're working on — no manual loading needed.

To verify installation:

```
/plugin
```

### GitHub Copilot CLI

Add the marketplace:

```
/plugin marketplace add microsoft/Agents
```

Then install the plugins you need:

```
/plugin install agents-sdk-common@microsoft-agents-sdk
/plugin install agents-for-js@microsoft-agents-sdk
/plugin install agents-for-net@microsoft-agents-sdk
```

---

## How Skills Work

Skills are Markdown files with a YAML frontmatter block that defines a `name` and `description`. The `description` is used by the AI assistant to decide when to activate the skill — it acts as a trigger condition.

When a skill activates, its full content is loaded into the assistant's context, giving it precise knowledge of the SDK's APIs, patterns, and common mistakes.

To browse skill content directly, see the [`agents-sdk-common/skills/`](./agents-sdk-common/skills/), [`agents-for-js/skills/`](./agents-for-js/skills/), and [`agents-for-net/skills/`](./agents-for-net/skills/) directories.
