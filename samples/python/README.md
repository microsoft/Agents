# Microsoft 365 Agents Python SDK Samples list

|Name|Description|README|
|----|----|----|
|Quickstart|Simplest agent|N/A|
|Auto Sign In|Simple OAuth agent using Graph and GitHub|[auto-signin](auto-signin/README.md)|
|OBO Authorization|OBO flow to access a Copilot Studio Agent|[obo-authorization](obo-authorization/README.md)|
|Semantic Kernel Integration|A weather agent built with Semantic Kernel|[semantic-kernel-multiturn](semantic-kernel-multiturn/README.md)|
|Streaming Agent|Streams OpenAI responses|[azure-ai-streaming](azure-ai-streaming/README.md)|
|Copilot Studio Client|Console app to consume a Copilot Studio Agent|[copilotstudio-client](copilotstudio-client/README.md)|
|Cards Agent|Agent that uses rich cards to enhance conversation design |[cards](cards/README.md)|
|Copilot Studio Skill|Call the echo bot from a Copilot Studio skill |[copilotstudio-skill](copilotstudio-skill/README.md)|

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
