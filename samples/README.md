# Microsoft 365 Agents SDK Samples

This catalog helps you choose a sample by language, scenario, and expected maturity. Most samples in this repository are starter samples: they are intentionally small, focused examples for learning one SDK concept or integration path. They are not production reference implementations unless explicitly labeled that way.

## Sample maturity tiers

| Tier | Use when you want to | What to expect |
|------|----------------------|----------------|
| **Tier 1: QuickStart** | Learn the SDK messaging loop and get a minimal agent running quickly. | Minimal code, local setup instructions, and explicit development-time shortcuts. |
| **Tier 2: Scenario starter** | Start from a realistic feature or integration scenario. | A focused sample for auth, telemetry, cards, streaming, Copilot Studio, tool orchestration, or another SDK capability. |
| **Tier 3: Production reference** | Understand a deployable architecture pattern. | Durable state, managed identity or secret management, health checks, observability, tests, infrastructure as code, deployment guidance, and a runbook. |

## Choose a sample

| Language | Sample list | Best first sample |
|----------|-------------|-------------------|
| .NET | [dotnet](dotnet/README.md) | [QuickStart](dotnet/quickstart/README.md) |
| JavaScript | [nodejs](nodejs/README.md) | [QuickStart](nodejs/quickstart/README.md) |
| Python | [python](python/README.md) | [Quickstart](python/quickstart/README.md) |

## Starter sample contract

Every starter sample should make its scope clear:

- State the maturity tier, language, scenario, supported channels, and approximate setup time.
- Name intentional shortcuts, such as in-memory state, local-only secrets, disabled token validation, or omitted retry policies.
- Explain what the sample does and does not demonstrate.
- Link to the next sample or documentation a developer should use when moving toward production.
- Include a lightweight smoke test or manual verification step that confirms the agent starts and responds.

Use [SAMPLE_README_TEMPLATE.md](SAMPLE_README_TEMPLATE.md) when adding or refreshing sample documentation.

## Current production reference coverage

The repository includes one Tier 3 production reference sample:

- **.NET support triage agent** ([dotnet/production-reference](dotnet/production-reference/README.md)) — a deployable, stateful support triage agent with managed identity, durable Blob Storage state, health checks, OpenTelemetry, Bicep infrastructure, xUnit tests, and a runbook.


Before labeling a sample as Tier 3, it should include or link to:

| Capability | Expected production-reference coverage |
|------------|----------------------------------------|
| Identity and secrets | Microsoft Entra configuration, managed identity where possible, Key Vault or equivalent secret storage, and no checked-in secrets. |
| State and storage | Durable conversation or user state with local development and cloud deployment configuration. |
| Observability | Structured logs, traces, metrics, correlation IDs, and a documented telemetry backend. |
| Reliability | Error handling, retry/throttling guidance, and health/readiness endpoints. |
| Deployment | Infrastructure as code, CI/CD guidance, rollback notes, and environment-specific configuration. |
| Testing | Unit tests, integration or smoke tests, and clear local validation commands. |
| Operations | Troubleshooting notes, a short runbook, and known limitations. |

## Starter gaps to prioritize

The current catalog has strong coverage for quickstarts, authentication, OpenTelemetry, streaming, Copilot Studio integration, orchestration scenarios, and **persistent state** ([.NET](dotnet/persistent-state/README.md), [JavaScript](nodejs/persistent-state/README.md), [Python](python/persistent-state/README.md)). The next starter samples that would most improve the path from "hello world" to production are:

| Gap | Why it matters |
|-----|----------------|
| Configuration and secret management starter | Shows local development settings separately from cloud-hosted secret storage. |
| Error handling, retry, and throttling starter | Shows realistic handling for downstream API failures and rate limits. |
| Health and readiness starter | Shows the endpoints and checks needed by hosting platforms and deployment pipelines. |
| Evaluation and regression testing starter | Shows how to validate prompts, tools, and agent behavior before deployment. |
| Cross-language parity set | Keeps the core QuickStart, auth, telemetry, and storage paths consistent across .NET, JavaScript, and Python. |
