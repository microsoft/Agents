# ADK Agent - Brand Search Optimization

**A2A Producer** built with Google ADK (Agent Development Kit)

---

## 🚀 Quick Start

### Prerequisites
- Python 3.11+
- Poetry (dependency management)
- Google Cloud Project ID
- Gemini API key from [AI Studio](https://aistudio.google.com/apikey)

### Installation

```bash
# 1. Clone and navigate
cd adk-agent

# 2. Install dependencies
poetry install

# 3. Configure environment
cp ../env.example .env
# Edit .env with your credentials
```

### Environment Variables

Create `.env` file:
```bash
GOOGLE_CLOUD_PROJECT=your-project-id    # Required - For BigQuery
GOOGLE_API_KEY=your-api-key             # Required - From AI Studio
MODEL=gemini-2.0-flash                  # Forever Free tier
GOOGLE_GENAI_USE_VERTEXAI=0             # Use ML Dev API (not Vertex AI)

# Optional
SERPAPI_KEY=your-serpapi-key            # 100 free searches/month
DISABLE_WEB_DRIVER=0                    # Set to 1 to disable web scraping
```

### Run CLI Mode

```bash
poetry run adk run brand_search_optimization

# Example interaction:
> Nike
[Agent displays categories: Active, Socks, Swim...]
> Active
[Agent extracts keywords...]
> continue
[Agent searches competitors and provides SEO recommendations]
```

### Run A2A Server Mode

```bash
python run_a2a.py

# Access endpoints:
# http://localhost:8080/.well-known/agent-card.json  (Agent Card)
# http://localhost:8080/health                        (Health Check)
# http://localhost:8080/invoke                        (A2A Invoke)
```

Test the agent card:
```bash
curl http://localhost:8080/.well-known/agent-card.json
```

---

## 📁 Project Structure

```
adk-agent/
├── brand_search_optimization/     # Main agent code
│   ├── agent.py                  # Root agent orchestration
│   ├── prompt.py                 # System prompts
│   ├── sub_agents/               # Sub-agent implementations
│   │   ├── keyword_finding/     # Keyword extraction
│   │   ├── search_results/      # Competitor intelligence
│   │   └── comparison/          # SEO analysis
│   ├── tools/                    # Tool implementations
│   │   ├── bq_connector.py      # BigQuery integration
│   │   └── serp_connector.py    # SerpAPI integration
│   └── shared_libraries/
│       └── constants.py          # Configuration constants
├── deployment/                    # Deployment scripts
│   ├── run.sh                    # Local run script
│   ├── eval.sh                   # Evaluation script
│   └── deploy.py                 # Vertex AI deployment
├── eval/                          # Evaluation datasets
│   └── data/
│       ├── eval_data1.evalset.json
│       └── test_config.json
├── tests/                         # Unit tests
│   └── unit/
│       └── test_tools.py
├── run_a2a.py                    # A2A server entry point
├── Dockerfile                     # Container image
├── pyproject.toml                 # Dependencies
└── README.md                      # This file
```

---

## 🔧 Configuration

### BigQuery Setup

The agent uses Google's **public dataset** - no setup required!

Dataset: `bigquery-public-data.thelook_ecommerce.products`

**Brands available**: Nike, Adidas, Levi's, Calvin Klein, Columbia, Puma, Under Armour, Reebok, and 100+ more.

### SerpAPI Setup (Optional)

For production-quality competitor data:

1. Sign up at [SerpAPI](https://serpapi.com/)
2. Get free API key (100 searches/month)
3. Add to `.env`: `SERPAPI_KEY=your_key_here`

Without SerpAPI, the agent uses web scraping fallback (slower, may encounter bot detection).

### Web Scraping Setup

**Local (Windows ARM64)**:
- Uses Firefox (better ARM64 support)
- Install Firefox: `winget install Mozilla.Firefox`

**Cloud (Linux x86_64)**:
- Uses Chrome (standard in containers)
- Dockerfile includes Chrome setup
- Set `DISABLE_WEB_DRIVER=1` for Cloud Run serverless

---

## 🚢 Deployment

### Option 1: Local Development

```bash
# CLI mode
poetry run adk run brand_search_optimization

# A2A server mode
python run_a2a.py
```

### Option 2: Docker

```bash
# Build
docker build -t brand-search-optimization .

# Run
docker run -p 8080:8080 \
  -e GOOGLE_API_KEY=$GOOGLE_API_KEY \
  -e GOOGLE_CLOUD_PROJECT=$GOOGLE_CLOUD_PROJECT \
  -e SERPAPI_KEY=$SERPAPI_KEY \
  brand-search-optimization

# Test
curl http://localhost:8080/.well-known/agent-card.json
```

### Option 3: Google Cloud Run

```bash
# Deploy from source
gcloud run deploy brand-search-agent \
  --source . \
  --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars GOOGLE_CLOUD_PROJECT=$GOOGLE_CLOUD_PROJECT,\
MODEL=gemini-2.0-flash,\
DISABLE_WEB_DRIVER=1

# Get URL
gcloud run services describe brand-search-agent \
  --region us-central1 \
  --format 'value(status.url)'
```

### Option 4: Vertex AI Agent Engine

```bash
# Set staging bucket
export STAGING_BUCKET=your-bucket-name

# Deploy
adk deploy brand_search_optimization \
  --project your-project \
  --location us-central1

# Expose as A2A
adk a2a expose brand_search_optimization \
  --project your-project \
  --location us-central1
```

See [A2A_DEPLOYMENT.md](../docs/ARCHITECTURE.md) for deployment architecture details.

---

## 🧪 Testing

### Unit Tests

```bash
poetry run pytest tests/unit/test_tools.py -v
```

### Integration Test

```bash
poetry run adk run brand_search_optimization

# Test workflow:
> Nike
[Verify categories displayed]
> Active
[Verify keywords extracted]
> continue
[Verify competitor data fetched]
> continue
[Verify SEO recommendations generated]
```

### Evaluation

```bash
adk eval brand_search_optimization \
  eval/data/eval_data1.evalset.json \
  --config_file_path eval/data/test_config.json
```

### A2A Endpoint Test

```bash
# Start server
python run_a2a.py

# In another terminal:
curl http://localhost:8080/health
curl http://localhost:8080/.well-known/agent-card.json

# Test invoke
curl -X POST http://localhost:8080/invoke \
  -H "Content-Type: application/json" \
  -d '{"message": "I want to optimize Nike products"}'
```

---

## 🔍 Troubleshooting

### Import Errors

```bash
# Ensure you're in the adk-agent directory
cd adk-agent

# Reinstall dependencies
poetry install --no-cache
```

### BigQuery Permission Errors

```bash
# Authenticate with Google Cloud
gcloud auth application-default login

# Verify project ID
gcloud config get-value project
```

### Web Scraping Failures

1. **Enable SerpAPI**: Add `SERPAPI_KEY` to `.env` (preferred solution)
2. **Check Firefox**: Ensure Firefox installed for local development
3. **Disable scraping**: Set `DISABLE_WEB_DRIVER=1` for serverless deployments

### Gemini API Rate Limits

- Free tier: 1500 requests/day
- Monitor usage in [Google AI Studio](https://aistudio.google.com/)
- Consider paid tier for production: $0.075/1M tokens

### Debug Logs

```bash
# Local
tail -f C:\Users\<USER>\AppData\Local\Temp\agents_log\agent.*.log

# Cloud Run
gcloud run logs tail brand-search-agent --region us-central1
```

---

## 📊 Performance Metrics

### Typical Execution Times
- Category selection: ~2 seconds
- Keyword extraction: ~5 seconds
- Competitor search (SerpAPI): ~3 seconds
- Competitor search (web scraping): ~15-30 seconds
- SEO analysis: ~10 seconds

**Total**: 25-50 seconds per workflow

### Token Usage
- Category selection: ~500 tokens
- Keyword extraction: ~2,000 tokens
- Search results: ~1,500 tokens
- Comparison report: ~3,000 tokens

**Total**: ~7,000 tokens per audit (free tier supports 200+ audits/day)

---

## 🎨 Customization

See [../customization.md](../docs/ARCHITECTURE.md) for architecture details and extension patterns.

---

## Documentation

- **[Root README](../README.md)** — Overall A2A reference implementation
- **[Client Agent README](../a2a-client-agent/README.md)** — M365 SDK + Semantic Kernel consumer
- **[A2A Patterns](../docs/A2A_PATTERNS.md)** — Protocol deep dive with sequence diagrams
- **[Architecture](../docs/ARCHITECTURE.md)** — System design details

---

## 🔗 Resources

- [Google ADK Documentation](https://google.github.io/adk-docs/)
- [A2A Protocol](https://a2a-protocol.org/)
- [BigQuery Public Datasets](https://cloud.google.com/bigquery/public-data)
- [SerpAPI Documentation](https://serpapi.com/docs)
- [Gemini API](https://ai.google.dev/gemini-api/docs)

---

## 📄 License

Apache 2.0 - See ../LICENSE file
