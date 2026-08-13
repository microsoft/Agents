# Microsoft 365 Agents Python SDK Samples list

These samples are Tier 1 or Tier 2 starter samples unless a README explicitly labels the sample as a production reference. See the [sample maturity tiers](../README.md#sample-maturity-tiers) before copying a sample into an application.

|Name|Tier|Description|README|
|----|----|----|----|
|Quickstart|Tier 1: QuickStart|Simplest agent|[quickstart](quickstart/README.md)|
|Auto Sign In|Tier 2: Scenario starter|Simple OAuth agent using Graph and GitHub|[auto-signin](auto-signin/README.md)|
|OBO Authorization|Tier 2: Scenario starter|OBO flow to access a Copilot Studio Agent|[obo-authorization](obo-authorization/README.md)|
|Semantic Kernel Integration|Tier 2: Scenario starter|A weather agent built with Semantic Kernel|[semantic-kernel-multiturn](semantic-kernel-multiturn/README.md)|
|Streaming Agent|Tier 2: Scenario starter|Streams OpenAI responses|[azureai-streaming](azureai-streaming/README.md)|
|Copilot Studio Client|Tier 2: Scenario starter|Console app to consume a Copilot Studio Agent|[copilotstudio-client](copilotstudio-client/README.md)|
|Cards Agent|Tier 2: Scenario starter|Agent that uses rich cards to enhance conversation design |[cards](cards/README.md)|
|Copilot Studio Skill|Tier 2: Scenario starter|Call the echo bot from a Copilot Studio skill |[copilotstudio-skill](copilotstudio-skill/README.md)|
|OpenTelemetry|Tier 2: Scenario starter|Instrument an agent and consume telemetry via the Aspire dashboard|[opentelemetry](otel/README.md)|
|Agent Framework|Tier 2: Scenario starter|Weather agent built with Microsoft Agent Framework SDK|[agent-framework](agent-framework/README.md)|
|Persistent State|Tier 2: Scenario starter|Durable conversation state backed by Azure Blob Storage|[persistent-state](persistent-state/README.md)|
|Copilot SDK|Tier 2: Scenario starter|Dungeon Scribe RPG agent powered by the GitHub Copilot SDK|[copilot-sdk](copilot-sdk/README.md)|

## Important Notice - Import Changes

> **⚠️ Breaking Change**: Recent updates have changed the Python import structure from `microsoft.agents` to `microsoft_agents` (using underscores instead of dots). Please update your imports accordingly.

### Import Examples

```python
# Activity types and models
from microsoft_agents.activity import Activity

# Core hosting functionality
from microsoft_agents.hosting.core import TurnContext

# aiohttp hosting
from microsoft_agents.hosting.aiohttp import start_agent_process

# Teams-specific functionality (compatible only with activity handler)
from microsoft_agents.hosting.teams import TeamsActivityHandler

# Azure Blob storage
from microsoft_agents.storage.blob import BlobStorage

# CosmosDB storage
from microsoft_agents.storage.cosmos import CosmosDbStorage

# MSAL authentication
from microsoft_agents.authentication.msal import MsalAuth

# Copilot Studio client
from microsoft_agents.copilotstudio.client import CopilotClient
```
