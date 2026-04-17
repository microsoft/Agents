# Brand Intelligence Advisor — A2A Client Agent

An M365 Agents SDK agent that communicates with the Google ADK Brand Search Optimization agent via the **A2A (Agent-to-Agent) protocol**, with **Semantic Kernel** as the LLM orchestrator.

## Architecture

```
User (Teams / WebChat / Test CLI)
              │
              ▼
     ┌────────────────────────────────────────────┐
     │  Brand Intelligence Advisor                │
     │  M365 Agents SDK + Semantic Kernel         │
     │  Port 3978                                 │
     │                                            │
     │  ┌──────────────────────────────────────┐  │
     │  │  Semantic Kernel Orchestrator        │  │
     │  │  ChatCompletionAgent (Azure OpenAI)  │  │
     │  │                                      │  │
     │  │  Tools (via @kernel_function):       │  │
     │  │   - analyze_brand  → A2A Client      │  │
     │  │   - check_push_notifications         │  │
     │  │   - get_analysis_history             │  │
     │  │   - get_seo_glossary                 │  │
     │  └──────────────┬───────────────────────┘  │
     │                 │                           │
     │  ┌──────────────▼───────────────────────┐  │
     │  │  A2A Client                          │  │
     │  │   - message/send       (ping)        │  │
     │  │   - message/stream     (SSE)         │  │
     │  │   - send + webhook     (push)        │  │
     │  └──────────────┬───────────────────────┘  │
     └─────────────────┼──────────────────────────┘
                       │ A2A Protocol (JSON-RPC)
                       ▼
     ┌────────────────────────────────────────────┐
     │  Brand Search Optimization                 │
     │  (Google ADK + Gemini 2.0 Flash)           │
     │  Port 8080                                 │
     │                                            │
     │  Sub-agents:                               │
     │   - keyword_finding  (BigQuery)            │
     │   - search_results   (SerpAPI)             │
     │   - comparison       (Gemini LLM)          │
     └────────────────────────────────────────────┘
```

## How It Works

The Semantic Kernel `ChatCompletionAgent` receives the user's natural language message, reasons about intent, and calls the appropriate tool:

1. **User says**: *"How is Nike doing in shoes?"*
2. **SK reasons**: This is a brand analysis → call `analyze_brand(brand="Nike", category="Active", mode="ping")`
3. **Tool executes**: A2A Client sends `message/send` to the ADK agent
4. **SK synthesizes**: Raw data + strategic interpretation → response to user

When Azure OpenAI is not configured, the agent falls back to regex-based command routing (`ping <brand>`, `stream <brand>`, etc.).

## Project Structure

```
a2a-client-agent/
├── brand_intelligence_advisor/     # Main agent package
│   ├── __init__.py                 #   Package metadata
│   ├── agent.py                    #   M365 SDK AgentApplication & message handlers
│   ├── orchestrator.py             #   Semantic Kernel ChatCompletionAgent + tools
│   ├── prompt.py                   #   System prompt for the LLM
│   ├── server.py                   #   aiohttp server (M365 + webhook endpoints)
│   └── tools/                      #   Tool implementations
│       ├── __init__.py
│       ├── a2a_client.py           #     A2A protocol client (ping, stream, push)
│       └── brand_advisor.py        #     Domain knowledge, query parsing, formatting
├── run_server.py                   # Entry point (python run_server.py)
├── test_demo.py                    # Interactive test CLI with SK orchestrator
├── cli_test.py                     # Direct A2A protocol test CLI
├── requirements.txt                # Python dependencies
├── env.TEMPLATE                    # Environment variable template
└── README.md                       # This file
```

### Module Responsibilities

| Module | Purpose |
|--------|---------|
| `agent.py` | M365 SDK `AgentApplication` — registers message handlers, delegates to orchestrator or fallback |
| `orchestrator.py` | Creates Semantic Kernel `ChatCompletionAgent` with `BrandToolsPlugin` (4 tools with `@kernel_function`) |
| `prompt.py` | System prompt defining the advisor persona, A2A pattern selection rules, and response formatting |
| `server.py` | aiohttp server with `/api/messages` (M365) and `/a2a/webhook` (push notification receiver) |
| `tools/a2a_client.py` | Async HTTP client implementing A2A v0.3.0 — `send_message()`, `stream_message()`, `send_with_push()` |
| `tools/brand_advisor.py` | Domain knowledge: 30+ brands, category mapping, SEO glossary, query parsing, history tracking |

## Prerequisites

- Python 3.10+
- ADK agent running on port 8080 (see [ADK Agent README](../adk-agent/README.md))
- Azure OpenAI endpoint (optional — enables SK orchestrator; without it, regex fallback works)

## Quick Start

### 1. Install dependencies

```bash
cd a2a-client-agent
pip install -r requirements.txt
```

### 2. Configure environment

```bash
cp env.TEMPLATE .env
```

Edit `.env`:
```bash
# Required — point to the ADK agent
A2A_AGENT_URL=http://localhost:8080

# Optional — enables Semantic Kernel LLM orchestration
AZURE_AI_FOUNDRY_ENDPOINT=https://your-resource.services.ai.azure.com
AZURE_AI_FOUNDRY_API_KEY=your-api-key
AZURE_AI_FOUNDRY_MODEL=gpt-4o-mini
```

### 3. Start the ADK agent (in another terminal)

```bash
cd ../adk-agent
poetry run python run_a2a.py
```

### 4. Start this agent

```bash
python run_server.py
```

Output:
```
+------------------------------------------------------------------+
|  Brand Intelligence Advisor                                      |
|  M365 Agents SDK + Semantic Kernel + A2A Protocol                |
|                                                                  |
|  LLM Orchestration: ENABLED (Semantic Kernel + Azure OpenAI)     |
+------------------------------------------------------------------+
```

### 5. Test with the interactive CLI

```bash
python test_demo.py
```

## Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/messages` | POST | M365 SDK message processing (user <-> agent) |
| `/a2a/webhook` | POST | Receives A2A push notifications from ADK agent |
| `/a2a/webhook` | GET | Debug view of received push notifications |

## Semantic Kernel Integration

The orchestrator uses [Semantic Kernel](https://pypi.org/project/semantic-kernel/) 1.40+ with:

- **`AzureChatCompletion`** service connecting to Azure OpenAI
- **`ChatCompletionAgent`** with a system prompt defining the advisor persona
- **`BrandToolsPlugin`** exposing 4 tools via `@kernel_function`:

| Tool | What It Does |
|------|-------------|
| `analyze_brand` | Calls A2A Client → ADK agent; mode param selects ping/stream/push |
| `check_push_notifications` | Returns any push notifications received via webhook |
| `get_analysis_history` | Returns session history of past analyses |
| `get_seo_glossary` | Looks up SEO terms from the built-in glossary |

The agent decides which tool to call and which A2A pattern to use based on natural language understanding of the user's request.

## Fallback Mode (No LLM)

When `AZURE_AI_FOUNDRY_ENDPOINT` is not set, the agent uses regex-based command routing:

| Command | A2A Pattern |
|---------|-------------|
| `ping Nike socks` | `message/send` (synchronous) |
| `stream Adidas shoes` | `message/stream` (SSE) |
| `push Puma sneakers` | `message/send` + webhook |
| `status` | View webhook notifications |
| `help` / `glossary` / `strategy` / `history` | Local capabilities |

## Dependencies

```
microsoft-agents-hosting-aiohttp     # M365 SDK server
microsoft-agents-hosting-core        # M365 SDK core
microsoft-agents-authentication-msal # M365 SDK auth
semantic-kernel>=1.40.0              # LLM orchestration
openai>=1.30.0                       # Azure OpenAI client
httpx / httpx-sse                    # A2A HTTP client
pydantic>=2.0,<2.12                  # Data validation (SK compatibility)
```
