# Microsoft 365 Agents Python SDK Samples list

|Name|Description|README|
|----|----|----|
|Production reference|Bounded Web Chat support intake with managed identity, Blob state, tests and operating guidance|[production-reference](production-reference/README.md)|
|Quickstart|Simplest agent|N/A|
|Auto Sign In|Simple OAuth agent using Graph and GitHub|[auto-signin](auto-signin/README.md)|
|OBO Authorization|OBO flow to access a Copilot Studio Agent|[obo-authorization](obo-authorization/README.md)|
|Semantic Kernel Integration|A weather agent built with Semantic Kernel|[semantic-kernel-multiturn](semantic-kernel-multiturn/README.md)|
|Azure AI Streaming|Streams Azure OpenAI responses|[azure-ai-streaming](azure-ai-streaming/README.md)|
|Copilot Studio Client|Console app to consume a Copilot Studio Agent|[copilotstudio-client](copilotstudio-client/README.md)|
|Cards Agent|Agent that uses rich cards to enhance conversation design |[cards](cards/README.md)|
|Copilot Studio Skill|Call the echo bot from a Copilot Studio skill |[copilotstudio-skill](copilotstudio-skill/README.md)|
|OpenTelemetry|Instrument an agent and consume telemetry via the Aspire dashboard|[opentelemetry](otel/README.md)|
|Agent Framework|Weather agent built with Microsoft Agent Framework SDK|[agent-framework](agent-framework/README.md)|
|Copilot SDK|Dungeon Scribe RPG agent powered by the GitHub Copilot SDK|[copilot-sdk](copilot-sdk/README.md)|
|Entra Agent ID Sidecar|Simple agent using the Microsoft Entra Agent ID sidecar for Agents SDK authentication|[entra-agent-id-sidecar](entra-agent-id-sidecar/README.md)|
|Build Genie Retrieval Agent|Retrieves SharePoint content with the signed-in user's delegated permissions.|[Build Genie Retrieval Agent](retrieval-agent/README.md)|
|Proactive|Sends messages to existing conversations from in-code and HTTP triggers|[proactive](proactive/README.md)|

## SDK 1.10 configuration

The hosted samples target the upcoming Python SDK **1.10.0** configuration APIs.
At the time of this update, that version is unreleased. Use matching preview
packages from the SDK checkout/build until 1.10.0 is published; installing an
older released SDK will not support these samples. The console-only
`copilotstudio-client` sample does not use this hosting configuration.

For preview wheels, run this from an activated virtual environment in the
selected sample directory:

```powershell
python -m pip install --find-links C:\path\to\preview-wheels -r requirements.txt
```

The wheel set must contain matching versions of the required SDK packages;
pinned samples expect `1.10.0` version metadata. Do not mix older SDK packages
with the preview or suppress deprecation warnings as a compatibility strategy.

### One root configuration, explicit adapters

Load `.env` before reading the environment, then reuse the **same root**
`Configuration` for connections, user authorization, and inbound JWT validation:

```python
from os import environ

from dotenv import load_dotenv
from microsoft_agents.activity.config import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import AgentApplication, Authorization, MemoryStorage

load_dotenv()
config = load_configuration_from_env(environ)
storage = MemoryStorage()
connections = MsalConnectionManager(config=config)
adapter = CloudAdapter(connection_manager=connections)
authorization = Authorization(storage, connections, config=config)
agent = AgentApplication(storage=storage, authorization=authorization)

# In the aiohttp application that uses jwt_authorization_middleware:
# app["agent_configuration"] = config
# app["agent_app"] = agent
# app["adapter"] = adapter
```

Pass `adapter` and `config` explicitly to the sample's `start_server`.
`AgentApplication` no longer owns the adapter. In route handlers use
`context.adapter`; for out-of-turn operations pass the host's adapter explicitly.
Keep the full root configuration in `app["agent_configuration"]`, not the
default provider's settings: JWT validation also needs `TokenValidation` and
the other configured connections.

Existing environment names such as
`CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID`,
`CONNECTIONSMAP__0__CONNECTION`, and
`AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__GRAPH__SCOPES` remain supported.
`load_configuration_from_env` expands `__`/`::` nesting and retains only SDK
sections (`Connections`, `ConnectionsMap`, `TokenValidation`,
`AgentApplication`, `Logging`, and `Port`). Read unrelated model, retrieval,
storage, and OTEL variables separately rather than expecting them in this object.

### Typed reads and settings

`Configuration` preserves key casing and raw values. Direct string/list lookups
are case-sensitive; use `ignore_case` when the source's casing can vary:

```python
from microsoft_agents.activity.config import Configuration, ignore_case

options = Configuration({"Enabled": "false", "Server": {"Timeout": "30"}})
enabled = options.get_bool(ignore_case("enabled"))  # False, not bool("false").
timeout = options.get_int(ignore_case("server", "timeout"))  # 30.
```

Use lists of path segments for nested reads; use `wildcard()` for collection
queries, not the literal `"*"`. Typed getters reject invalid nonempty values
even when a default is supplied. Indexed scope variables such as `SCOPES__0`
bind automatically; do not rewrite the loaded configuration to turn them into
lists.

For code-defined settings, use `MsalConnectionSettings`,
`SidecarConnectionSettings`, `JwtConnectionSettings`, `TokenValidationConfig`,
`AuthorizationConfig`, and `AuthHandlerConfig` as appropriate.
Provider settings bind **one connection** with `from_config`, whereas managers
and `Authorization` accept the application root through `config=`.

| Deprecated path | Replacement |
|---|---|
| `MsalConnectionManager(**config)` / `Authorization(..., **config)` | Explicit `config=config` |
| `AgentAuthConfiguration` for provider construction | Typed provider settings or a connection `Configuration` |
| Provider `.configuration` | Provider `.config` with typed, snake-case fields |
| `get_default_connection_configuration()` for JWT hosting | The original root `Configuration` |
| `AuthHandler(...)` | `AuthHandlerConfig(...)` or `AuthHandlerConfig.from_config(...)` |
| `AgentApplication(adapter=...)` / `agent.adapter` | Host-owned adapter / `context.adapter` during a turn |

Production must still explicitly disable anonymous access and validate the
expected audience, issuer, tenant, and token family. An explicit
`TokenValidation` audience list limits accepted audiences; without it, the SDK
uses the configured connection client IDs. See the
[production reference](production-reference/README.md) for a bounded policy
and JWT rejection tests.

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
