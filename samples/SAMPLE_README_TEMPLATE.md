# Sample README Template

Use this template when adding a new Microsoft 365 Agents SDK sample or refreshing an existing sample README. Keep starter samples focused and explicit about what is intentionally omitted.

````markdown
# <Sample name>

> **Sample tier:** Tier 1: QuickStart | Tier 2: Scenario starter | Tier 3: Production reference
> **Language:** .NET | JavaScript | Python
> **Scenario:** <one-line scenario>
> **Estimated setup time:** <for example, 10 minutes>
> **Supported channels:** <Agents Playground, Web Chat, Teams, Microsoft 365 Copilot, custom app, etc.>

## What this sample demonstrates

- <Capability or concept this sample teaches>
- <Integration, channel, or SDK behavior shown by the sample>

## What this sample does not demonstrate

- <Production capability intentionally omitted, such as durable state>
- <Security, deployment, testing, or operations concern outside the sample scope>

## Prerequisites

- <Runtime and version>
- <Required tools>
- <Required cloud resources, if any>

## Configure the sample

1. <Configuration step>
1. <Configuration step>

## Run locally

```bash
<command>
```

## Verify the sample

1. <Start the agent or test client>
1. <Send a message or trigger the scenario>
1. Confirm: `<expected observable result>`

## Intentional shortcuts

| Shortcut | Why it is acceptable in this sample | Production alternative |
|----------|-------------------------------------|------------------------|
| <Example: local `.env` secret> | <Keeps local setup simple> | <Use Key Vault or equivalent secret storage> |

## Next steps

- <Link to the next starter sample>
- <Link to relevant Microsoft Learn documentation>
- <Link to production guidance or checklist>
````

## Tier 3 production-reference additions

If the sample is labeled Tier 3, also include:

- Architecture diagram or deployment topology.
- Durable state and storage configuration.
- Managed identity or equivalent non-local secret flow.
- Health/readiness endpoints.
- OpenTelemetry or equivalent structured observability.
- Unit, integration, and smoke test commands.
- Infrastructure-as-code deployment instructions.
- Rollback and troubleshooting notes.
- Known limits and support boundaries.
