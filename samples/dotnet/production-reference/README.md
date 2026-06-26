# Production Reference — Support Triage Agent

> **Sample tier:** Tier 3: Production reference
> **Language:** .NET
> **Scenario:** Stateful support triage agent — no LLM, durable conversation state, managed identity, health checks, telemetry, and infrastructure as code
> **Estimated setup time:** 30 minutes (local) · 60 minutes (Azure)
> **Supported channels:** Agents Playground, Web Chat, Teams, Microsoft 365 Copilot, custom app

## What this sample demonstrates

- A multi-turn support triage workflow that collects issue summary, impact, and contact preference without an LLM.
- Durable conversation-scoped state backed by Azure Blob Storage with two runtime paths:
  - **Local development:** connection string to Azurite or a real storage account.
  - **Cloud deployment:** managed identity (`DefaultAzureCredential`) via `AZURE_BLOB_STORAGE_CONTAINER_URI`.
- Azure Bot Service / Entra token validation in production via `TokenValidation__*` configuration.
- OpenTelemetry for ASP.NET Core, HttpClient, .NET runtime, and the Agents SDK meter and activity source, with OTLP and Azure Monitor export paths.
- Health endpoints (`/health/live`, `/health/ready`) used by App Service health check configuration.
- Infrastructure as code (`infra/main.bicep`) that provisions App Service, Blob Storage, Key Vault, Application Insights, Log Analytics, and all required role assignments.
- xUnit test suite covering dialog logic, storage configuration options, and health endpoint behavior.

## What this sample does not demonstrate

- LLM or AI inference — all dialog decisions are deterministic rule-based logic.
- Multi-region or geo-redundant storage configuration.
- State schema versioning or migration strategies.
- Slot-based blue/green deployments or advanced CI/CD pipelines.
- User-scoped state (only conversation-scoped state is used).
- Proactive messaging or outbound channel calls.

## Architecture summary

The agent runs as a Linux .NET 8 App Service with a system-assigned managed identity. Incoming messages from Azure Bot Service arrive at `POST /api/messages`, are validated by the Agents SDK token middleware, and are routed to `SupportTriageAgent`. The agent loads per-conversation state from Azure Blob Storage, advances the triage dialog by one step, persists the updated state, and returns a reply.

**Runtime components:**

| Component | Role |
|-----------|------|
| App Service (Linux .NET 8) | Hosts the agent; health check path set to `/health/live` |
| Azure Blob Storage (`agents-state` container) | Durable conversation state |
| Key Vault (`BotClientSecret`) | Bot client secret resolved at startup via App Service Key Vault reference |
| Application Insights + Log Analytics | Receives traces, metrics, and logs via Azure Monitor OpenTelemetry distro |
| Azure Bot Service (pre-existing) | Validates channel traffic; configured with `messagingEndpoint` output after deploy |

**Managed identity role assignments provisioned by Bicep:**

- `Storage Blob Data Contributor` scoped to the blob container — enables read/write of state blobs.
- `Key Vault Secrets User` scoped to the Key Vault — enables Key Vault reference resolution for `BotClientSecret`.

## Production coverage checklist

| Capability | Coverage in this sample |
|------------|------------------------|
| Identity and secrets | Entra token validation; system-assigned managed identity; bot client secret in Key Vault; no checked-in secrets |
| State and storage | Durable conversation state in Blob Storage; local (Azurite / connection string) and cloud (managed identity) paths |
| Observability | OpenTelemetry traces, metrics, and logs; OTLP and Azure Monitor export; Application Insights |
| Reliability | Health and readiness endpoints; fail-fast storage validation on startup; no in-memory fallback |
| Deployment | Bicep IaC; documented deployment sequence; role assignments automated |
| Testing | xUnit unit and integration tests for dialog, storage options, and health endpoints |
| Operations | This README, [DEPLOYMENT.md](DEPLOYMENT.md), and [RUNBOOK.md](RUNBOOK.md) |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local storage emulation, **or** an Azure Storage account with a blob container
- An Azure Bot registration (required to connect a real channel; not required for local testing with [Agents Playground](https://learn.microsoft.com/azure/bot-service/bot-service-overview))
- For Azure deployment: [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) with the Bicep extension, and an Azure subscription

## Configure the sample

### Local — Azurite emulator

Start Azurite and set the development connection string:

```bash
npx azurite --silent
```

```bash
# Bash
export AZURE_BLOB_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true"
```

```powershell
# PowerShell
$env:AZURE_BLOB_STORAGE_CONNECTION_STRING = "UseDevelopmentStorage=true"
```

### Local — real Azure Blob Storage

```bash
export AZURE_BLOB_STORAGE_CONNECTION_STRING="<your-storage-connection-string>"
```

Optionally override the container name (default is `agents-production-reference-state`):

```bash
export AZURE_BLOB_STORAGE_CONTAINER_NAME="my-agent-state"
```

### Token validation (required when connecting a real channel)

Edit `appsettings.json` or set environment variables:

- Set `TokenValidation__Enabled` to `true`.
- Replace `{{ClientId}}` with your bot app ID and `{{TenantId}}` with your Entra tenant ID.
- Configure `Connections__ServiceConnection__Settings__AuthType`, `ClientId`, `ClientSecret`, and `AuthorityEndpoint` to match your bot registration.

For local secrets, use [.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "TokenValidation__Enabled" "true" --project samples/dotnet/production-reference/ProductionReference.csproj
dotnet user-secrets set "Connections__ServiceConnection__Settings__ClientSecret" "<your-secret>" --project samples/dotnet/production-reference/ProductionReference.csproj
```

## Run locally

```bash
cd samples/dotnet/production-reference
dotnet run
```

The agent starts on `http://localhost:3978` by default.

## Verify the sample

### Health endpoints

```bash
curl http://localhost:3978/health/live
# Expected: 200 OK (liveness probe; no dependency checks)

curl http://localhost:3978/health/ready
# Expected: 200 OK with {"status":"Healthy"} when storage is configured correctly
# Expected: 503 Service Unavailable when storage configuration is missing or invalid
```

### Message flow

1. Open [Agents Playground](https://learn.microsoft.com/azure/bot-service/bot-service-overview) or Bot Framework Emulator and connect to `http://localhost:3978/api/messages`.
2. Send any message.
3. Confirm: the agent asks "Please describe your issue."
4. Send an issue description.
5. Confirm: the agent asks about impact level.
6. Send an impact level.
7. Confirm: the agent asks about contact preference.
8. Send a preference.
9. Confirm: the agent returns a summary of the collected information.

### State persistence

1. Complete one full triage conversation.
2. Stop and restart the agent process (`Ctrl+C`, then `dotnet run`).
3. In the same conversation (same conversation ID), send another message.
4. Confirm: the agent returns the final summary again, proving that state was restored from Blob Storage.

## Azure deployment

See [DEPLOYMENT.md](DEPLOYMENT.md) for the full deployment command sequence, parameter details, managed identity explanation, and post-deploy validation steps.

## Run tests

```bash
dotnet build samples/dotnet/production-reference/ProductionReference.csproj
dotnet test samples/dotnet/production-reference/tests/ProductionReference.Tests.csproj
```

Validate the Bicep template locally (generates `main.json` locally; keep that file untracked):

```bash
az bicep build --file samples/dotnet/production-reference/infra/main.bicep
```

## Intentional shortcuts

| Shortcut | Why it is acceptable in this sample | Production alternative |
|----------|-------------------------------------|------------------------|
| Single-region Blob Storage (`Standard_LRS`) | Sufficient for a reference sample; simplifies IaC | Use `Standard_GRS` or `Standard_RAGRS` for multi-region resiliency |
| `B1` App Service plan | Low-cost reference deployment | Right-size to `P1v3` or higher for production throughput |
| Manual bot registration | Bot Service provisioning varies significantly across organizations | Automate with a separate Bicep module or Azure CLI script |
| `softDeleteRetentionInDays: 7` on Key Vault | Minimum allowed; acceptable for a sample | Set to 30–90 days in production |
| Storage configuration validated on startup only | Keeps the health check fast and avoids live Azure calls in tests | Add a live blob-level probe for deeper readiness validation |

## Known limits

- Azure Blob Storage optimistic concurrency is not explicitly handled in `AgentStorageFactory`; high-concurrency scenarios on the same conversation may see contention.
- State blobs are not cleaned up after a conversation completes; configure a Blob Storage lifecycle management policy for retention in production.
- The triage dialog has no abandonment timeout; long-idle conversations retain their state indefinitely.

## Next steps

- **Simpler starting point:** [Persistent State](../persistent-state/README.md) — Tier 2 scenario starter that shows Blob Storage state without the full production infrastructure.
- **Operations:** [RUNBOOK.md](RUNBOOK.md) — health interpretation, telemetry queries, common incidents, and rollback guidance.
- **Deploy to Azure:** [DEPLOYMENT.md](DEPLOYMENT.md) — complete deployment sequence and post-deploy validation.
- **Authorize blobs with managed identity:** [Authorize access to blobs using managed identities](https://learn.microsoft.com/azure/storage/blobs/authorize-managed-identity)
- **OpenTelemetry .NET:** [Getting started with OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- **Azure Monitor OpenTelemetry distro:** [Enable Azure Monitor OpenTelemetry for .NET](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable?tabs=net)
