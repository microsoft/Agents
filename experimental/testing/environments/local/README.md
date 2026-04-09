# Local Environment

Runs agent tests inside a single Docker container. All three agent runtimes
(Python, Node.js, .NET) plus PowerShell are present in the image. Each test
starts the agent as a subprocess via its `run_agent.ps1`, sends activities through
the `microsoft-agents-testing` client, and asserts on the responses. No cloud
hosting is involved — only an Azure Bot registration and Key Vault for credentials.

## Directory layout

```
local/
├── agents/                     # Agent source for every scenario
│   └── quickstart/
│       ├── python/             # run_agent.ps1, requirements.txt, src/, env.TEMPLATE
│       ├── js/                 # run_agent.ps1, package.json, src/, env.TEMPLATE
│       └── net/                # run_agent.ps1, *.csproj, appsettings.json
├── infra/                      # Bicep: App Registration + Azure Bot + Key Vault
├── scripts/
│   ├── provision.ps1           # Provision Azure resources (non-azd path)
│   ├── postprovision.ps1       # azd postprovision hook: generate secret + inject config
│   ├── predown.ps1             # azd predown hook: delete App Registration before azd down
│   ├── inject_config.py        # Template substitution (${{ VAR }}) for config files
│   └── run_local.ps1           # Build Docker image + run tests (entry point)
├── tests/
│   └── test_quickstart.py
├── azure.yaml                  # azd config (optional — for `azd provision`)
└── conftest.py                 # Stub (scenarios wired directly in test files)
```

## Prerequisites

| Tool | Purpose |
|---|---|
| [Docker](https://docs.docker.com/get-docker/) | Builds and runs the test container |
| [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | Provision and tear down Azure resources |
| [Azure CLI (`az`)](https://learn.microsoft.com/cli/azure/install-azure-cli) | Called by azd hooks for Graph and Key Vault operations |
| `az login` + `azd auth login` | Active sessions before provisioning |

No local Python/Node/.NET installation is required — everything runs inside the container.

## Quickstart

### 1. Provision Azure resources

From `environments/local/`:

```powershell
cd environments/local
azd env new e2e-local      # create a named environment (once per environment)
azd provision
```

This deploys an App Registration, Azure Bot, and Key Vault, generates a client secret,
stores it in Key Vault, and writes credentials into the agent config files (`.env`,
`appsettings.local.json`).

Resources are reused on subsequent runs — `azd provision` is idempotent.

### 2. Run tests

```powershell
# From the repo root (testing/):
./environments/local/scripts/run_local.ps1
```

Builds `environments/local/tests.Dockerfile` (tagged `agents-test`) and runs pytest inside the
container with the repo mounted at `/repo`.

### 3. Tear down

```powershell
cd environments/local
azd down
```

Deletes the App Registration (via `predown` hook) and then removes the resource group
(Azure Bot, Key Vault). Re-provision from scratch with `azd provision`.

---

### Alternative: `provision.ps1` (no `azd` required)

For CI runners or environments where `azd` is not available:

```powershell
./environments/local/scripts/provision.ps1 -ResourceGroup rg-e2e-local
```

Manual teardown: `az group delete -n rg-e2e-local && az ad app delete --id <APP_ID>`.

---

### `run_local.ps1` options

| Flag | Description |
|---|---|
| `-Provision` | Run `provision.ps1` before tests (non-azd path). Requires `-ResourceGroup`. |
| `-ResourceGroup <rg>` | Azure resource group (required with `-Provision`). |
| `-EnvironmentName <n>` | Bicep environment name suffix. Default: `e2e-local`. |
| `-Location <region>` | Azure region. Default: `eastus`. |
| `-Scenario <name>` | Agent scenario folder. Default: `quickstart`. |
| `-TestPath <path>` | pytest path inside the container. Default: `environments/local/tests/`. |
| `-NoBuild` | Skip `docker build` and reuse the existing `agents-test` image. |

## How it works

1. `provision.ps1` deploys Bicep → writes `infra-outputs.json` → calls `inject_config.py`
   → fills `${{ VAR }}` placeholders in `env.TEMPLATE` / `appsettings.json` → writes
   `.env` / `appsettings.local.json` next to each agent.

2. `run_local.ps1` builds `environments/local/tests.Dockerfile` and runs:
   ```
   docker run --rm -v "<repo>:/repo" agents-test environments/local/tests/
   ```

3. Inside the container, `uv run pytest` installs the test framework from `uv.lock`
   and runs the test suite.

4. For each test class, the `microsoft_agents.testing` plugin reads the
   `@pytest.mark.agent_test(<scenario>)` marker and injects an `agent_client` fixture
   that manages the agent lifecycle:
   - Starts the agent subprocess via `pwsh run_agent.ps1` (cwd = agent directory)
   - Waits for the agent to become ready
   - Provides an `AgentClient` for sending activities and asserting responses
   - Terminates the subprocess when the test completes

## Agent configuration

Each agent reads credentials from a file in its own directory:

| Language | Config file | Template |
|---|---|---|
| Python | `.env` | `env.TEMPLATE` |
| Node.js | `.env` | `env.TEMPLATE` |
| .NET | `appsettings.local.json` | `appsettings.json` |

These files are written by `inject_config.py` during provisioning and are gitignored.
To inspect or override individual values, edit them directly after provisioning.

## Adding a new scenario

1. Create `environments/local/agents/<scenario>/<language>/run_agent.ps1` that
   installs dependencies and starts the agent on `http://localhost:3978/api/messages`.
2. Add `env.TEMPLATE` (or `appsettings.json` for .NET) with `${{ VAR }}` placeholders
   for any credentials needed.
3. Add a test file under `environments/local/tests/test_<scenario>.py` following the
   `BaseTest` + per-language subclass pattern in `test_quickstart.py`.
4. If the scenario needs a longer startup delay, pass `delay=<seconds>` to
   `create_scenario()`.
