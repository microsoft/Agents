# M365 Agents SDK — A2A Pattern Reference Implementation

A **sample reference implementation** demonstrating three complementary capabilities within the M365 Agents SDK ecosystem. Each capability is presented as a distinct learning track — with its own files, concepts, and business rationale — so developers can study them independently or follow them end-to-end.

> **Scope**: The domain (brand search optimization) is intentionally non-trivial so pattern selection has real consequences. The business logic is secondary; the architectural patterns are the subject matter.

---

## Three Learning Tracks

This repository covers three areas that are often needed together but serve different engineering concerns:

| # | Track | Core Question | Key Outcome |
|---|-------|---------------|-------------|
| 1 | [Intelligent Agent Orchestration](#track-1-intelligent-agent-orchestration-with-semantic-kernel) | *How do I add LLM reasoning and tool calling to an M365 agent?* | A Semantic Kernel `ChatCompletionAgent` that plans, invokes tools, and synthesizes responses |
| 2 | [Agent-to-Agent Communication](#track-2-agent-to-agent-communication-via-a2a-protocol) | *How does an M365 agent consume a remote agent over A2A?* | A fully wired A2A client (discovery → send → receive) inside an M365 Agents SDK host |
| 3 | [A2A Transmission Patterns](#track-3-a2a-transmission-patterns--choosing-the-right-delivery-model) | *Which A2A delivery model — sync, stream, or push — fits my use case?* | Three working patterns with test CLIs that exercise each one independently |

---

### Track 1: Intelligent Agent Orchestration with Semantic Kernel

**Business context** — Enterprise agents need more than hard-coded routing. A brand strategist might type *"Compare Nike and Adidas in the running shoe category and recommend an SEO action plan"* — a request that requires intent parsing, orchestrating multiple tools in sequence, and composing a coherent, strategic response. Semantic Kernel provides the planning layer that turns a natural-language query into a structured tool-calling workflow backed by Azure OpenAI.

| Concept | Implementation | File |
|---------|---------------|------|
| `ChatCompletionAgent` setup with Azure OpenAI | Agent construction, service wiring, instruction prompt | [orchestrator.py](a2a-client-agent/brand_intelligence_advisor/orchestrator.py) |
| `@kernel_function` tool declarations | 4 tools: `analyze_brand`, `check_push_notifications`, `get_analysis_history`, `get_seo_glossary` | [orchestrator.py](a2a-client-agent/brand_intelligence_advisor/orchestrator.py) |
| System prompt engineering | Role definition, tool-usage instructions, response formatting | [prompt.py](a2a-client-agent/brand_intelligence_advisor/prompt.py) |
| M365 SDK message handler integration | Routing incoming messages to the SK orchestrator | [agent.py](a2a-client-agent/brand_intelligence_advisor/agent.py) |
| Graceful degradation (no LLM) | Falls back to regex-based command routing when Azure OpenAI is unavailable | [agent.py](a2a-client-agent/brand_intelligence_advisor/agent.py) |

**What you will learn**: How to wire `AzureChatCompletion` into an SK agent, expose domain functions as `@kernel_function` tools, and let the LLM autonomously decide which tools to call and in what order — all within the M365 Agents SDK hosting model.

---

### Track 2: Agent-to-Agent Communication via A2A Protocol

**Business context** — No single agent has all the data. A brand intelligence advisor (M365 SDK) needs SEO analysis from a specialist agent (Google ADK) that has access to BigQuery datasets and SerpAPI. The [A2A protocol](https://google.github.io/a2a/#/) provides a vendor-neutral, JSON-RPC-based contract for this cross-platform communication — agent discovery, message exchange, task lifecycle, and push notifications — without coupling the two systems.

| Concept | Implementation | File |
|---------|---------------|------|
| Agent discovery (`/.well-known/agent-card.json`) | `discover()` fetches the remote agent's capabilities | [a2a_client.py](a2a-client-agent/brand_intelligence_advisor/tools/a2a_client.py) |
| Message send (`message/send`) | `send_message()` posts a JSON-RPC request, parses the response | [a2a_client.py](a2a-client-agent/brand_intelligence_advisor/tools/a2a_client.py) |
| Message stream (`message/stream`) | `stream_message()` consumes an SSE event stream | [a2a_client.py](a2a-client-agent/brand_intelligence_advisor/tools/a2a_client.py) |
| Push notification registration | `register_push()` sets `pushNotificationConfig` with a webhook URL | [a2a_client.py](a2a-client-agent/brand_intelligence_advisor/tools/a2a_client.py) |
| Webhook receiver endpoint | `/a2a/webhook` POST handler stores incoming push notifications | [server.py](a2a-client-agent/brand_intelligence_advisor/server.py) |
| A2A producer (other side of the protocol) | Google ADK agent exposing A2A endpoints | [adk-agent/](adk-agent/) |

**What you will learn**: How to implement a complete A2A client lifecycle — discover a remote agent, send it work, consume results via three different transports, and receive asynchronous push notifications — all hosted inside an M365 Agents SDK application.

---

### Track 3: A2A Transmission Patterns — Choosing the Right Delivery Model

**Business context** — The *same* query (*"Analyze Nike"*) can be delivered three different ways depending on the operational context. A brand manager checking rankings mid-meeting needs a fast, blocking response (ping). An analyst building a quarterly report wants to see data arrive progressively (stream). A marketing director scheduling overnight batch audits across 15 categories needs fire-and-forget with a webhook callback (push). Selecting the wrong pattern degrades user experience or wastes infrastructure.

#### Pattern Reference

| Pattern | A2A Method | Transport | Behavior |
|---------|-----------|-----------|----------|
| **Ping** | `message/send` | HTTP POST → JSON response | Blocks until complete |
| **Stream** | `message/stream` | HTTP POST → SSE event stream | Delivers chunks in real time |
| **Push** | `message/send` + `pushNotificationConfig/set` | HTTP POST + webhook callback | Returns immediately; result arrives asynchronously |

#### When to Use Each Pattern

| Pattern | Best For | Typical Scenario | Anti-Pattern |
|---------|----------|-----------------|--------------|
| **Ping** | Quick lookups, chatbot replies, CI pipeline checks | *"How is Nike ranking?"* before a call — answer needed in 30–60 s | Analysis takes >2 min (user stares at a spinner) |
| **Stream** | Detailed reports, live dashboards, executive demos | Quarterly review — analyst reads early findings while deeper analysis runs | Client cannot consume SSE events |
| **Push** | Batch jobs, overnight audits, mobile requests | *"Audit 10 brands"* then walk away — webhook notifies when done | Quick questions (webhook overhead not justified) |

#### Decision Flowchart

```
User sends a query
        │
        ▼
Is the user actively waiting?
        │
    ┌───┴───┐
   YES      NO ──── Use PUSH (fire & forget + webhook)
    │
    ▼
Need real-time progress?
    │
┌───┴───┐
YES     NO ──────── Use PING (simple request/response)
│
▼
Use STREAM (SSE chunks as they arrive)
```

#### Test CLIs for Each Pattern

| CLI | Command | What It Tests | Needs Azure OpenAI? |
|-----|---------|---------------|---------------------|
| `test_demo.py` | `python test_demo.py` | All 3 patterns via SK orchestrator — LLM selects pattern | Yes |
| `cli_test.py` | `python cli_test.py ping "Nike socks"` | Individual pattern, direct A2A call, no LLM layer | No |
| `cli_test.py` | `python cli_test.py all "Nike shoes"` | All 3 patterns sequentially | No |

> **LLM-Orchestrated selection**: When Azure OpenAI is configured, the Semantic Kernel orchestrator reads user intent and selects the pattern automatically. *"Quick check on Nike"* → ping. *"Detailed report on Adidas"* → stream. *"Run this overnight"* → push.

<details>
<summary><strong>Expanded business scenarios for each pattern</strong></summary>

**Ping — Synchronous**
- **Pre-meeting pulse check**: Brand manager asks *"How is Nike ranking in Active?"* 5 min before a stakeholder call. Blocking is acceptable — the wait is short and the user is actively watching.
- **Teams bot integration**: User sends *"What's our share of voice for running shoes?"* in chat. Chatbot UX expects a single reply bubble — ping fits naturally.
- **CI pipeline validation**: Automated script verifies brand ranking hasn't dropped after a product title change.

**Stream — Real-Time**
- **Quarterly competitive review**: *"Full analysis of Adidas in sportswear"* takes 60–90 s. Streaming lets the analyst read keyword data while deeper analysis continues.
- **Live dashboard**: SSE events drive real-time UI updates — each chunk paints a new row in the metrics table.
- **Executive demo**: Text appearing progressively creates a compelling *"AI thinking"* experience.

**Push — Asynchronous**
- **Batch brand audit**: *"Audit Puma across all 15 categories"* runs for 5+ min. Push returns immediately; webhook fires when done.
- **Nightly cron job**: Orchestrator fires 10 push requests in parallel. Webhook callbacks post results to a Slack channel — no polling.
- **Mobile analysis**: User requests deep analysis while commuting. Push avoids cellular timeout risk; notification arrives when results are ready.

</details>

**What you will learn**: How each A2A transmission pattern works at the protocol level, when to choose one over another, and how to test each independently using the provided CLIs.

---

## Architecture

```
┌──────────────────────────────────────┐                    ┌──────────────────────────────────────┐
│  Brand Intelligence Advisor          │                    │  Brand Search Optimization           │
│  (M365 Agents SDK + Semantic Kernel) │   A2A Protocol     │  (Google ADK + Gemini 2.0 Flash)     │
│  Port 3978                           │   (JSON-RPC)       │  Port 8080                           │
│                                      │                    │                                      │
│  Track 1: SK orchestrator + tools    │                    │  Multi-agent SEO analysis            │
│  Track 2: A2A client integration     │                    │  (keyword / search / comparison)     │
│  Track 3: Ping / Stream / Push       │                    │                                      │
│                                      │                    │                                      │
│  Pattern 1: message/send      (ping) │────────────────────▶  Synchronous blocking response       │
│  Pattern 2: message/stream  (stream) │────────────────────▶  Server-Sent Events (SSE)            │
│  Pattern 3: send + webhook    (push) │────────────────────▶  Background + push notification      │
│                                      │◀───────────────────│  Webhook callback with results       │
└──────────────────────────────────────┘                    └──────────────────────────────────────┘
```

---

## Repository Structure

```
m365-sdk-a2a-patterns/
├── a2a-client-agent/                          # A2A Consumer (M365 Agents SDK)
│   ├── brand_intelligence_advisor/            #   Main agent package
│   │   ├── __init__.py                        #     Package metadata
│   │   ├── agent.py                           #     M365 SDK routes & message handlers
│   │   ├── orchestrator.py                    #     Semantic Kernel LLM orchestrator
│   │   ├── prompt.py                          #     System prompt for the LLM
│   │   ├── server.py                          #     aiohttp server + webhook endpoint
│   │   └── tools/                             #     Tool implementations
│   │       ├── __init__.py
│   │       ├── a2a_client.py                  #       A2A protocol client (all 3 patterns)
│   │       └── brand_advisor.py               #       Domain knowledge & query parsing
│   ├── run_server.py                          #   Entry point (python run_server.py)
│   ├── test_demo.py                           #   Interactive test CLI (SK orchestrator)
│   ├── cli_test.py                            #   Direct A2A test CLI (no orchestrator)
│   ├── requirements.txt                       #   Python dependencies
│   └── env.TEMPLATE                           #   Environment variable template
│
├── adk-agent/                                 # A2A Producer (Google ADK)
│   ├── brand_search_optimization/             #   Multi-agent system
│   │   ├── agent.py                           #     Root agent orchestration
│   │   ├── prompt.py                          #     System prompts
│   │   ├── sub_agents/                        #     Sub-agent implementations
│   │   │   ├── keyword_finding/               #       Keyword extraction from BigQuery
│   │   │   ├── search_results/                #       Competitor intel via SerpAPI
│   │   │   └── comparison/                    #       SEO analysis with Gemini
│   │   ├── tools/                             #     BigQuery + SerpAPI connectors
│   │   └── shared_libraries/                  #     Constants & config
│   ├── run_a2a.py                             #   A2A server entry point
│   ├── pyproject.toml                         #   Dependencies (Poetry)
│   └── Dockerfile                             #   Container image
│
├── docs/                                      # Documentation
│   ├── A2A_PATTERNS.md                        #   A2A pattern deep dive with sequence diagrams
│   └── ARCHITECTURE.md                        #   System design and data flow
│
├── env.example                                # ADK agent environment template
└── README.md                                  # This file
```

---

## Quick Start

### Prerequisites

| Requirement | Purpose | How to Get |
|-------------|---------|------------|
| Python 3.10+ | Runtime | [python.org](https://www.python.org/downloads/) |
| Poetry 2.0+ | ADK agent dependencies | [python-poetry.org](https://python-poetry.org/) |
| Google Cloud project | BigQuery public dataset access | [console.cloud.google.com](https://console.cloud.google.com/) |
| Gemini API key | LLM for ADK agent (free) | [aistudio.google.com/apikey](https://aistudio.google.com/apikey) — **Forever Free tier** |
| Azure OpenAI endpoint | LLM orchestration (optional) | [Azure AI Foundry](https://ai.azure.com/) |

> **Free API Keys**: The Gemini API key is available on Google's Forever Free tier (1,500 requests/day). SerpAPI offers 100 free searches/month at [serpapi.com](https://serpapi.com/). No credit card required for either.

### Step 1: Start the ADK Agent (A2A Producer)

```bash
cd adk-agent
cp ../env.example .env
# Edit .env:
#   GOOGLE_API_KEY=your-gemini-api-key     (from aistudio.google.com/apikey)
#   GOOGLE_CLOUD_PROJECT=your-project-id   (for BigQuery)

poetry install
gcloud auth application-default login
python run_a2a.py
# Server running on http://localhost:8080
```

Verify: `curl http://localhost:8080/.well-known/agent-card.json`

### Step 2: Start the Client Agent (A2A Consumer)

```bash
cd a2a-client-agent
cp env.TEMPLATE .env
# Edit .env:
#   A2A_AGENT_URL=http://localhost:8080
#   AZURE_AI_FOUNDRY_ENDPOINT=https://your-resource.services.ai.azure.com  (optional)
#   AZURE_AI_FOUNDRY_API_KEY=your-key                                      (optional)

pip install -r requirements.txt
python run_server.py
# Server running on http://localhost:3978
```

> Without Azure OpenAI configured, the agent falls back to regex-based command routing (ping/stream/push commands still work).

### Step 3: Test with the Interactive CLI

```bash
cd a2a-client-agent
python test_demo.py
```

The interactive CLI lets you test all 3 patterns with the SK orchestrator:

```
============================================================
  A2A Interactive Test Runner
============================================================
  Orchestrator : Semantic Kernel + Azure OpenAI
  Remote Agent : Google ADK (A2A Protocol v0.3)
  Framework    : Microsoft 365 Agents SDK

You> How is Nike doing in Active category?
  Choose A2A transmission pattern:
    [1] Ping   - synchronous request/response
    [2] Stream - SSE live typing
    [3] Push   - fire & forget
    [4] Auto   - let the LLM decide (default)
  Mode [1/2/3/4, default=4]: 1

  [PING] Sending to SK orchestrator...
  [PING] Waiting for complete response...

Advisor (34.5s) [pattern: ping]:
  Based on the A2A analysis, here are the key findings for Nike
  in the Active category...
```

### Step 4: Test Directly with the CLI (No Orchestrator)

```bash
cd a2a-client-agent

# Agent discovery
python cli_test.py discover

# Test individual patterns
python cli_test.py ping "Nike socks"
python cli_test.py stream "Adidas shoes"
python cli_test.py push "Puma sneakers"
python cli_test.py status

# Run all patterns in sequence
python cli_test.py all "Nike running shoes"

# Interactive mode
python cli_test.py
```

---

## Security

- **No hardcoded secrets**: All API keys and credentials are loaded from environment variables via `.env` files.
- **`.env` gitignored**: The `.gitignore` covers `.env`, `*.bak`, `*.key`, and `credentials.json`.
- **env.TEMPLATE**: Use the provided templates (`env.TEMPLATE` for client, `env.example` for ADK) as a starting point. Fill in your own keys.
- **Anonymous mode**: The client agent runs without MSAL authentication by default (for local development). Set the `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__*` variables for authenticated mode.

---

## Endpoints

| Port | Endpoint | Method | Purpose |
|------|----------|--------|---------|
| 3978 | `/api/messages` | POST | M365 SDK message processing |
| 3978 | `/a2a/webhook` | POST | Receives push notifications from ADK agent |
| 3978 | `/a2a/webhook` | GET | Debug view of received notifications |
| 8080 | `/.well-known/agent-card.json` | GET | ADK agent card discovery |
| 8080 | `/` | POST | A2A JSON-RPC endpoint (all patterns) |

---

## Documentation

| Guide | Description |
|-------|-------------|
| [A2A Patterns Deep Dive](docs/A2A_PATTERNS.md) | Sequence diagrams, JSON-RPC payloads, and state machines for all 3 patterns |
| [Architecture](docs/ARCHITECTURE.md) | System design, data flow, and technology stack |
| [ADK Agent README](adk-agent/README.md) | ADK agent setup, deployment, and customization |
| [Client Agent README](a2a-client-agent/README.md) | Client agent architecture, SK orchestrator, and configuration |

---

## Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Client Agent** | M365 Agents SDK (Python) | Message handling, Teams/WebChat integration |
| **LLM Orchestration** | Semantic Kernel 1.40+ | Tool calling, prompt management, agent reasoning |
| **LLM Model** | Azure OpenAI (gpt-4o-mini) | Intent understanding, response synthesis |
| **A2A Protocol** | JSON-RPC 2.0 over HTTP | Agent-to-agent communication |
| **Producer Agent** | Google ADK 1.23+ | Multi-agent SEO analysis workflow |
| **Producer LLM** | Gemini 2.0 Flash | SEO analysis and recommendations (free tier) |
| **Data Source** | BigQuery public dataset | Product catalog (thelook_ecommerce) |
| **Search Data** | SerpAPI | Competitor search results (100 free/month) |

---

## Further Reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) — Parent repository with additional samples and documentation
- [Agents SDK Documentation](https://aka.ms/M365-Agents-SDK-Docs) — Official docs
- [Agents for Python](https://github.com/microsoft/agents-for-python) — Python SDK source
- [A2A Protocol Specification](https://google.github.io/a2a/#/) — Agent-to-Agent protocol spec
- [Google ADK Documentation](https://google.github.io/adk-docs/) — Agent Development Kit docs
- [Semantic Kernel (Python)](https://pypi.org/project/semantic-kernel/) — SK orchestration framework

---

## Disclaimer

This is a **sample reference implementation** for learning and demonstration purposes. It shows how M365 Agents SDK agents can use the A2A protocol with different transmission patterns. It is not production-ready — users are responsible for security hardening, error handling, and deployment considerations before using in production.

---

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit [https://cla.opensource.microsoft.com](https://cla.opensource.microsoft.com/).

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

## License

[MIT](LICENSE)
