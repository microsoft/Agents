# Architecture Documentation

## System Overview

The Brand Search Optimization Agent is a multi-agent system built with Google ADK that analyzes retail product SEO and provides optimization recommendations.

## Agent Architecture

### Root Agent Orchestration

```
Root Agent (brand_search_optimization)
├── BigQuery Connector Tool
│   └── Retrieves product data from public dataset
│
├── Keyword Finding Agent
│   └── Extracts high-value keywords from product titles
│
├── Search Results Agent
│   ├── SerpAPI Connector Tool (primary)
│   └── Web Scraping via Selenium (fallback)
│
└── Comparison Root Agent
    ├── Generator Agent
    │   └── Analyzes keyword usage patterns
    └── Critic Agent
        └── Provides actionable SEO recommendations
```

## Multi-Agent Design Pattern

**Pattern:** Router Agent

The root agent acts as an intelligent router, directing the conversation through specialized sub-agents based on workflow state:

1. **Category Selection** → BigQuery Tool
2. **Keyword Extraction** → Keyword Finding Agent  
3. **Competitor Research** → Search Results Agent
4. **SEO Analysis** → Comparison Root Agent

## Data Flow

```
┌─────────────┐
│    User     │
│  "Nike      │
│   socks"    │
└──────┬──────┘
       │
       ▼
┌──────────────────────────┐
│    Root Agent            │
│  (Orchestration)         │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│  BigQuery Tool           │
│  Query: brand="Nike"     │
│  category="Socks"        │
└──────┬───────────────────┘
       │
       ▼ Products (13 items)
┌──────────────────────────┐
│  Keyword Finding Agent   │
│  Extract: "moisture      │
│  wicking", "cushioned"   │
└──────┬───────────────────┘
       │
       ▼ Keywords
┌──────────────────────────┐
│  Search Results Agent    │
│  ├─ Try: SerpAPI        │
│  └─ Fallback: Selenium   │
└──────┬───────────────────┘
       │
       ▼ Competitor data
┌──────────────────────────┐
│  Comparison Agent        │
│  ├─ Generator: Patterns  │
│  └─ Critic: SEO Tips    │
└──────┬───────────────────┘
       │
       ▼
┌─────────────┐
│    User     │
│  SEO Report │
└─────────────┘
```

## A2A Integration

### Protocol Implementation

The agent exposes the A2A protocol via the simplified `to_a2a()` utility:

```python
# adk-agent/run_a2a.py
from google_adk.a2a import to_a2a

root_agent = agent_config.build_root_agent()
app = to_a2a(root_agent)
```

### Endpoints

| Endpoint | Purpose | Method |
|----------|---------|--------|
| `/.well-known/agent-card.json` | Agent metadata | GET |
| `/invoke` | Conversation API | POST |
| `/health` | Health check | GET |

### Agent Card Structure

```json
{
  "name": "Brand Search Optimization Agent",
  "description": "Analyzes product SEO and provides optimization recommendations",
  "capabilities": ["conversation", "multi-turn"],
  "authentication": "none",
  "endpoints": {
    "invoke": "https://your-url/invoke"
  }
}
```

## Technology Stack

### Core Framework
- **Google ADK** 1.23.0 - Multi-agent orchestration
- **Gemini 2.0 Flash** - LLM (Forever Free tier)
- **Uvicorn** - ASGI server for A2A protocol

### Data Sources
- **BigQuery** - Public dataset (thelook_ecommerce)
- **SerpAPI** - Production competitor data
- **Selenium** - Web scraping fallback

### Dependencies
```toml
google-generativeai = "^0.8.3"
google-adk = {extras = ["a2a"], version = "^1.23.0"}
google-cloud-bigquery = "^3.27.0"
uvicorn = "^0.32.1"
google-search-results = "^2.4.2"
selenium = "^4.27.1"
```

## State Management

### Conversation State
The agent maintains state across turns using ADK's built-in session management:

- **User context**: Brand, category, keywords
- **Workflow stage**: Category selection → Keywords → Competitors → Report
- **Previous responses**: Enables "continue" command

### Stateless A2A
Each A2A request includes full conversation history in the payload, making the server stateless for horizontal scaling.

## Performance Characteristics

### Timing Breakdown
- Category selection: 3-5s
- Keyword extraction: 5-8s  
- Competitor search: 10-15s
- SEO analysis: 7-10s

**Total workflow:** 25-35s

### Resource Usage
- **Memory:** ~200MB per request
- **CPU:** Minimal (LLM calls are external)
- **Network:** 2-5MB per full workflow

### Scaling Considerations
- **Stateless design** → Easy horizontal scaling
- **BigQuery caching** → Reduces query costs
- **SerpAPI rate limits** → 100 free searches/month
- **Free tier limits** → 1500 Gemini requests/day

## Security Architecture

### Secrets Management
- API keys in `.env` (never committed)
- Environment variables in production
- `.gitignore` protects sensitive files

### Network Security
- Dev Tunnel for local testing only
- HTTPS enforced in production
- CORS configured for Copilot Studio

All credentials are loaded from `.env` files (gitignored). See env templates for required variables.

## Deployment Patterns

### Local Development
```
User → test_demo.py / cli_test.py → Client Agent → A2A → ADK Agent → Tools
```

### M365 Integration
```
User → Teams / WebChat → M365 Agents SDK → A2A → ADK Agent → Tools
```

### Production
```
User → Teams → Azure Bot Service → Client Agent → HTTPS → Cloud Run → ADK Agent
```

## A2A Client Agent Architecture

The client agent uses Semantic Kernel for LLM orchestration:

```
brand_intelligence_advisor/
├── agent.py          # M365 SDK AgentApplication, message handlers
├── orchestrator.py   # SK ChatCompletionAgent + BrandToolsPlugin
├── prompt.py         # System prompt (advisor persona)
├── server.py         # aiohttp server (M365 + webhook endpoints)
└── tools/
    ├── a2a_client.py   # A2A protocol client (ping, stream, push)
    └── brand_advisor.py # Domain knowledge, query parsing, formatting
```

### SK Tool Flow
1. User message → `agent.py` → `orchestrator.process_message()`
2. SK reasons about intent → calls `@kernel_function` tools
3. `analyze_brand()` → `A2AClient.send_message()` / `.stream_message()` / `.send_with_push()`
4. SK synthesizes raw data into strategic response

## Cost Optimization

### Free Tier Strategy
- **Gemini:** 1500 requests/day (Forever Free)
- **BigQuery:** Public dataset (no cost)
- **SerpAPI:** 100 searches/month free

**Result:** 1000+ brand audits/day at $0 cost

## References

- [A2A Protocol Specification](https://google.github.io/a2a/#/)
- [Google ADK Documentation](https://google.github.io/adk-docs/)
- [M365 Agents SDK](https://pypi.org/project/microsoft-agents-hosting-core/)
- [Semantic Kernel](https://pypi.org/project/semantic-kernel/)
- [BigQuery Public Datasets](https://cloud.google.com/bigquery/public-data)
- [SerpAPI Documentation](https://serpapi.com/docs)
