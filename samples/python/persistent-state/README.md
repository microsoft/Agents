# Persistent State

> **Sample tier:** Tier 2: Scenario starter
> **Language:** Python
> **Scenario:** Durable conversation state backed by Azure Blob Storage
> **Estimated setup time:** 15 minutes
> **Supported channels:** Agents Playground, Web Chat, Teams, Microsoft 365 Copilot, custom app

## What this sample demonstrates

- How to configure `BlobStorage` backed by Azure Blob Storage so that conversation state persists across agent restarts.
- How to read and write per-conversation state values using `TurnState`.
- A simple turn counter that increments on every message and survives process recycling.

## What this sample does not demonstrate

- Managed identity or workload identity authentication to Blob Storage (connection string only; see [Next steps](#next-steps) for the production path).
- Multi-region or geo-redundant storage configuration.
- State schema versioning or migration.
- User-scoped state (only conversation-scoped state is shown).

## Prerequisites

- Python 3.11 or later
- An Azure Storage account with a Blob container, or [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local emulation
- An Azure Bot registration (required to connect a channel; not required for local emulator testing with [Agents Playground](https://learn.microsoft.com/azure/bot-service/bot-service-overview))

## Configure the sample

1. Create an Azure Storage account (or start Azurite for local development):

   ```bash
   # Azurite emulator — no Azure account needed for local testing
   npx azurite --silent
   ```

2. Copy `env.TEMPLATE` to `.env` in the `samples/python/persistent-state` directory and fill in the values:

   ```bash
   cp samples/python/persistent-state/env.TEMPLATE samples/python/persistent-state/.env
   ```

   Then edit `.env`:

   ```env
   # Required — Azure Storage connection string
   # For Azurite (local emulator):
   AZURE_BLOB_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true

   # For a real Azure Storage account:
   # AZURE_BLOB_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...

   # Optional — override the default container name (default: agents-persistent-state)
   # AZURE_BLOB_STORAGE_CONTAINER_NAME=my-agent-state
   ```

   The default container name is `agents-persistent-state`. The container is created automatically by the SDK on first use.

3. *(When connecting a real channel)* Configure authentication for your Azure Bot registration by setting the appropriate environment variables or extending the app configuration.

## Run locally

```bash
cd samples/python/persistent-state

# Create and activate a virtual environment
python -m venv .venv

# Windows
.venv\Scripts\activate

# macOS/Linux
# source .venv/bin/activate

pip install -r requirements.txt
python -m src.main
```

## Verify the sample

1. Open [Agents Playground](https://learn.microsoft.com/azure/bot-service/bot-service-overview) and connect to `http://localhost:3978/api/messages`.
2. Send any message.
3. Confirm: the reply reads `[1] you said: <your text>`.
4. Send another message.
5. Confirm: the counter increments — `[2] you said: <your text>`.
6. Stop and restart the agent process, then send a message.
7. Confirm: the counter continues from where it left off, demonstrating that state was persisted to Blob Storage.

## Intentional shortcuts

| Shortcut | Why it is acceptable in this sample | Production alternative |
|----------|-------------------------------------|------------------------|
| Connection string for storage auth | Simplest local setup; works with Azurite | [Managed identity](https://learn.microsoft.com/azure/storage/blobs/authorize-managed-identity) or [workload identity](https://learn.microsoft.com/azure/aks/workload-identity-overview) — instantiate `BlobStorage` with a credential from `azure-identity` instead of a connection string |
| `agents-persistent-state` default container | Avoids a configuration step for quick evaluation | Choose a container name that reflects the workload and environment |
| No state schema versioning | Counter is a primitive type; schema evolution is trivial | Version state keys or use a migration strategy for complex state objects |
| Local `.env` file for secrets | Keeps local setup simple; file is git-ignored | Use Azure Key Vault, environment variables injected by the platform, or a managed identity credential chain |

## Next steps

- **Cloud deployment with managed identity:** Assign the `Storage Blob Data Contributor` role to your agent's managed identity on the container, then instantiate `BlobStorage` with a `DefaultAzureCredential` from the [`azure-identity`](https://pypi.org/project/azure-identity/) package instead of a connection string; add `azure-identity` to `requirements.txt` for that path. See [Authorize access to blobs using managed identities](https://learn.microsoft.com/azure/storage/blobs/authorize-managed-identity).

- **Workload identity on AKS:** Follow the [Workload identity overview](https://learn.microsoft.com/azure/aks/workload-identity-overview) to federate a Kubernetes service account with an Entra application and use `DefaultAzureCredential` without any secrets in the pod spec.

- **State at scale:** Review [Azure Blob Storage scalability targets](https://learn.microsoft.com/azure/storage/common/scalability-targets-standard-account) and consider [Cosmos DB partitioned storage](https://learn.microsoft.com/azure/cosmos-db/introduction) for higher-throughput scenarios.

- **Next starter sample:** [Copilot SDK](../copilot-sdk/README.md) — learn how to build more advanced agents using the Copilot SDK.
