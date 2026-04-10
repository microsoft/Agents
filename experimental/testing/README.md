# E2E Testing Infrastructure — Implementation Plan

## Overview

Two test environments, a shared tooling layer, and a containerized test runner.
Tests are environment-agnostic; infra wiring is environment-specific.

**Two distinct Docker strategies — one per environment:**

| Environment | Docker model | Rationale |
|---|---|---|
| `local` | Single multi-runtime image | Agents are subprocesses; one image hosts Python + Node + .NET + pwsh. Scales to N agents × 3 languages without configuration explosion. Dependencies install at runtime (same as a dev machine). |
| `cloud` | One image per language | Agents are long-lived services addressable by URL. Separate images enable independent rebuild, restart, and scaling. Maps directly to ACI container groups. |

---

## Implementation Status

| Phase | Description | Status |
|---|---|---|
| 1 | `DeployedInfra` dataclass | ⬜ Not started |
| 2 | `docker/local.Dockerfile` | ✅ Done |
| 2 | Cloud Dockerfiles (python/node/dotnet) | ⬜ Not started |
| 3 | Bicep modules (ACR, ACI) | ⬜ Not started |
| 4 | `provision.ps1` cloud steps | ⬜ Not started |
| 5 | `run_local.ps1` (local test runner) | ✅ Done |
| 5 | Cloud `Dockerfile.tests` | ⬜ Not started |
| 6 | `environments/local/` wiring | ✅ Done |
| 6 | `environments/cloud/` wiring | ⬜ Not started |
| 7 | `pytest.ini` update | ✅ Done |
| — | Agent migration (`src/agents/` → `environments/local/agents/`) | ✅ Done |

---

## Current Repository Layout

> This reflects the **current state** of the POC. The `environments/local/` environment
> is self-contained; cloud infra and the `src/common/` cloud helpers are not yet built.

```
testing/
├── _tests/                                 # Unit tests for shared tooling
├── docker/
│   └── local.Dockerfile                    # ✅ Multi-runtime image: Python 3.13 + Node 22 + .NET 8 + pwsh
├── environments/
│   ├── local/                              # ✅ Fully self-contained local environment
│   │   ├── agents/                         # Agent source (moved from src/agents/)
│   │   │   └── quickstart/
│   │   │       ├── python/                 # .env, requirements.txt, run_agent.ps1, src/
│   │   │       ├── js/                     # .env, package.json, run_agent.ps1, src/
│   │   │       └── net/                    # appsettings.json, run_agent.ps1, *.csproj
│   │   ├── conftest.py                     # Stub (scenarios wired via @pytest.mark.agent_test)
│   │   ├── infra/                          # Bicep: App Registration + Azure Bot + Key Vault
│   │   ├── scripts/
│   │   │   ├── inject_config.py            # Template substitution for agent config files
│   │   │   ├── provision.ps1               # Provisions Azure resources + injects config
│   │   │   ├── postprovision.ps1           # azd hook variant of provision
│   │   │   └── run_local.ps1              # ✅ Build image + run tests (optional: -Provision)
│   │   ├── azure.yaml                      # azd config (postprovision hook)
│   │   └── tests/
│   │       └── test_quickstart.py
│   └── cloud/                              # ⬜ Not yet built
├── src/
│   └── common/                             # Shared Python library (centralized)
│       ├── _infra/                         # Stubs: builders, config writers, settings
│       ├── constants.py                    # AGENTS_PATH → environments/local/agents/
│       ├── source_scenario.py              # SourceScenario (subprocess runner)
│       ├── types.py                        # SDKVersion enum
│       └── utils.py                        # create_scenario(), create_agent_path()
├── pyproject.toml
├── pytest.ini                              # testpaths = environments/local/tests _tests
└── uv.lock
```

---

## Design Decisions (vs. original plan)

### Agents co-located with environment
Agents live at `environments/local/agents/` rather than `src/agents/` at the repo root.
All environment-specific concerns (agents, infra, scripts, azure.yaml) are self-contained
under `environments/local/`. `src/common/` stays centralized and shared.

### Volume-mount test runner (no `Dockerfile.tests`)
The original plan called for a `Dockerfile.tests` with test files baked in.
Instead, `docker/local.Dockerfile` is a bare multi-runtime image and the repo is
**mounted at runtime** (`-v "${PWD}:/repo"`). This eliminates a rebuild cycle every time
test files change and avoids a separate runner image per environment for local dev.

For the cloud environment, a baked-in `Dockerfile.tests` still makes sense (the image
needs to be pushed to ACR and run remotely), so the original plan applies there.

### `run_local.ps1` in `environments/local/scripts/`
The run script lives alongside the other local environment scripts rather than at the
repo root. It accepts `-Provision -ResourceGroup <rg>` to provision and test in one
command.

### `@pytest.mark.agent_test` (no `conftest.py` fixture wiring)
The local environment currently wires scenarios directly in test files via
`@pytest.mark.agent_test(<scenario>)`. The `conftest.py`-based `scenario` fixture
described in Phase 6 is deferred until the mark-based approach is validated.

---

## Phase 1 — Shared Tooling: `DeployedInfra` dataclass

**File:** `src/common/infra/outputs.py`

```python
@dataclass
class DeployedInfra:
    app_id: str
    tenant_id: str
    umi_client_id: str
    acr_login_server: str
    python_url: str
    node_url: str
    dotnet_url: str
    key_vault_name: str

    @classmethod
    def load(cls, path: Path = Path("infra-outputs.json")) -> "DeployedInfra": ...
```

- Loaded via `DeployedInfra.load()` — fails fast with a clear message if the file is
  missing (tells the user to run `provision.ps1` first).
- Exported from `src/common/__init__.py`.
- Unit-tested in `_tests/`.

---

## Phase 2 — Dockerfiles (`docker/`)

### `docker/local.Dockerfile` ✅

Multi-runtime image based on `python:3.13-slim` (Debian 12). All three runtimes +
PowerShell in one image. Agents are **not** pre-built into the image — they install
dependencies and run as subprocesses at test time, exactly like a dev machine.

```dockerfile
FROM python:3.13-slim

ENV DEBIAN_FRONTEND=noninteractive

# Node.js 22, .NET SDK 8, PowerShell via Microsoft packages (Debian 12)
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && curl -fsSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
         -o /tmp/pkgs.deb \
    && dpkg -i /tmp/pkgs.deb && rm /tmp/pkgs.deb \
    && apt-get update && apt-get install -y --no-install-recommends \
         dotnet-sdk-8.0 powershell \
    && rm -rf /var/lib/apt/lists/*

RUN pip install uv --break-system-packages

WORKDIR /repo
ENTRYPOINT ["uv", "run", "pytest"]
```

**Usage:**
```powershell
# From environments/local/:
./scripts/run_local.ps1                              # build + test
./scripts/run_local.ps1 -NoBuild                     # skip rebuild
./scripts/run_local.ps1 -Provision -ResourceGroup rg-e2e-local  # provision + test
```

**Port conflict note:** when running multiple agents in parallel each `SourceScenario`
must bind a unique port. Currently all agents use the default port 3978; tests run
sequentially so there is no conflict yet. Port allocation via a session-scoped fixture
is needed before parallel execution.

**Startup delay note:** `create_scenario()` defaults to 30s to accommodate first-run
`uv pip install` / `npm install` inside the container. Cached runs are much faster.

---

### `docker/python.Dockerfile`, `node.Dockerfile`, `dotnet.Dockerfile` — ⬜ cloud env

One image per language. Agent source and dependencies are baked in at build time
(`--build-arg SCENARIO=quickstart`). `inject_config.py` runs *before* `docker build`
so credentials are already in `.env` / `appsettings.local.json` when the image is
built. For UMI/FIC the managed identity is attached at the ACI level — no secret
baked in.

```dockerfile
# docker/python.Dockerfile
FROM python:3.13-slim
ARG SCENARIO
WORKDIR /app
COPY environments/local/agents/${SCENARIO}/python/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY environments/local/agents/${SCENARIO}/python/ .
EXPOSE 3978
CMD ["python", "-m", "src.main"]
```

```dockerfile
# docker/node.Dockerfile
FROM node:22-slim
ARG SCENARIO
WORKDIR /app
COPY environments/local/agents/${SCENARIO}/js/package*.json ./
RUN npm ci
COPY environments/local/agents/${SCENARIO}/js/ .
RUN npm run build
EXPOSE 3978
CMD ["node", "--env-file", ".env", "./dist/index.js"]
```

```dockerfile
# docker/dotnet.Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SCENARIO
WORKDIR /src
COPY environments/local/agents/${SCENARIO}/net/ .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 3978
ENTRYPOINT ["dotnet", "Quickstart.dll"]
```

---

## Phase 3 — New Bicep Modules ⬜

### `infra/modules/container-registry.bicep`
- Provisions an ACR (Basic SKU, sufficient for test images).
- Outputs: `loginServer`, `name`.
- The UMI is granted `AcrPull` role so ACI can pull without admin credentials.

### `infra/modules/container-instance.bicep`
- Parameters: `name`, `image`, `umiResourceId`, `envVars[]`, `port`.
- Provisions a single ACI container group.
- Attaches UMI for managed identity auth.
- TCP port 3978 exposed (or 443 with a sidecar — see note below).
- Outputs: `fqdn` (fully-qualified domain name ACI assigns).

### `infra/main.bicep` additions
```bicep
module acr './modules/container-registry.bicep' = { ... }

// One module call per language — all three deploy in parallel.
module aciPython './modules/container-instance.bicep' = {
  params: { name: 'aci-${envName}-python', image: '${acr.outputs.loginServer}/quickstart-python:latest', ... }
}
module aciNode './modules/container-instance.bicep' = { ... }
module aciDotnet './modules/container-instance.bicep' = { ... }
```

New outputs added: `ACR_LOGIN_SERVER`, `PYTHON_URL`, `NODE_URL`, `DOTNET_URL`.

---

## Phase 4 — `provision.ps1` steps (cloud environment) ⬜

```
1. az group create
2. az deployment group create (infra/main.bicep) → captures outputs
3. inject_config.py → writes .env and appsettings.local.json for each language
4. az acr login
5. docker build + docker push  (×3, in parallel via Start-Job)
6. az container delete + az container create  (or re-deploy via ACI image refresh)
7. environments/cloud/scripts/run_tests.ps1
```

Steps 5 and 6 are the "build + push + deploy" loop. On subsequent runs when only
agent code changes (not infra), only steps 3–6 need to run — infra is idempotent.

---

## Phase 5 — Test Runner ✅ (local) / ⬜ (cloud)

### Local: volume-mount approach (implemented)

`docker/local.Dockerfile` is the test runner. The repo is mounted at `/repo` at
runtime so test files and agent source don't need to be baked in.

```powershell
# environments/local/scripts/run_local.ps1 handles this:
docker build -f docker/local.Dockerfile -t agents-local .
docker run --rm -v "${RepoRoot}:/repo" agents-local environments/local/tests/
```

### Cloud: baked-in image (future)

For the cloud environment the test runner needs to be pushed to ACR and run remotely,
so a baked-in `Dockerfile.tests` still makes sense:

```dockerfile
FROM python:3.13-slim
WORKDIR /app
COPY pyproject.toml uv.lock ./
RUN pip install uv && uv sync
COPY src/common/ src/common/
COPY environments/ environments/
COPY _tests/ _tests/
COPY pytest.ini .
ENTRYPOINT ["uv", "run", "pytest"]
```

#### `environments/cloud/scripts/run_tests.ps1`
```powershell
param([string]$Environment = 'cloud', [string]$OutputsFile = 'infra-outputs.json')

docker build -f Dockerfile.tests -t agents-test-runner .
docker run --rm `
    -v "${PWD}/${OutputsFile}:/app/infra-outputs.json:ro" `
    agents-test-runner `
    "environments/$Environment"
```

---

## Phase 6 — Environment Wiring

### `environments/local/` ✅

Scenarios are currently declared directly in test files via `@pytest.mark.agent_test`:

```python
# environments/local/tests/test_quickstart.py
PYTHON_SCENARIO = create_scenario(AGENT_NAME, SDKVersion.PYTHON)

@pytest.mark.agent_test(PYTHON_SCENARIO)
class TestQuickstartPython(BaseTestQuickstart):
    pass
```

The `conftest.py` fixture-based approach from the original plan is deferred:

```python
# deferred — environments/local/conftest.py
@pytest.fixture(params=[PYTHON_SCENARIO, JS_SCENARIO, NET_SCENARIO], ids=[...])
def scenario(request):
    return request.param
```

### `environments/cloud/conftest.py` ⬜
```python
@pytest.fixture(scope="session")
def infra() -> DeployedInfra:
    return DeployedInfra.load()   # reads infra-outputs.json mounted into the container

@pytest.fixture(params=["python", "node", "dotnet"], scope="session")
def scenario(request, infra: DeployedInfra):
    url = {"python": infra.python_url, "node": infra.node_url, "dotnet": infra.dotnet_url}[request.param]
    return ExternalScenario(url=f"{url}/api/messages")
```

`ExternalScenario` is a new thin wrapper in `src/common/scenarios.py` that
implements the same async context manager protocol as `SourceScenario` but skips
process management — it just yields a `ClientFactory` pointed at the given URL.

### Shared test files

`environments/local/tests/test_quickstart.py` and
`environments/cloud/tests/test_quickstart.py` are **identical in content** — both
import and subclass `BaseTestQuickstart` from `src/common/`. The only difference
is which `conftest.py` wires up the `scenario` fixture.

---

## Phase 7 — `pytest.ini` update ✅

```ini
testpaths = environments/local/tests _tests
```

(Cloud testpath added once `environments/cloud/` is built.)

Running a single environment:
```sh
uv run pytest environments/local/     # local only
uv run pytest environments/cloud/     # cloud only (requires infra-outputs.json)
uv run pytest _tests/                 # unit tests only
```

---

## Migration from `src/` ✅

| Previous location | Current location |
|---|---|
| `src/agents/<scenario>/` | `environments/local/agents/<scenario>/` |
| `src/tests/basic/test_quickstart.py` | `environments/local/tests/test_quickstart.py` |
| `src/common/` | `src/common/` (unchanged, centralized) |

`src/agents/` and `src/tests/` are removed.

---

## Items from NEXT_STEPS not yet covered

### 1. Dev tunnel support (`TunneledSourceScenario`)

NEXT_STEPS planned a `TunneledSourceScenario` for scenarios (OAuth, Copilot Studio
skills) that require a publicly reachable HTTPS endpoint while the agent runs locally.

**Fits as a third environment:** `environments/local_tunneled/`

- `conftest.py` builds `TunneledSourceScenario` objects instead of `SourceScenario`
- `TunneledSourceScenario` (in `src/common/`) extends `SourceScenario`: starts
  `devtunnel host` as a second subprocess, captures the HTTPS URL from stdout,
  injects it into `ClientConfig.service_url` and the agent's `.env`, then tears
  down both processes on exit
- Prerequisite: `devtunnel login` must have been run once on the machine
- The Azure Bot resource (already provisioned by the local environment's provision
  step) gets its messaging endpoint updated to the tunnel URL via `az bot update --endpoint`

Not required for the quickstart scenario but needed before OAuth/skill tests can exist.

---

### 2. Ephemeral resource lifecycle (`--reuse-resources` / `--destroy-resources`)

NEXT_STEPS called for pytest flags to control whether provisioned Azure resources
are reused or torn down:

```
--reuse-resources    Skip provisioning if infra-outputs.json already exists
--destroy-resources  Run az group delete after the test session ends
```

Useful in CI (create → test → destroy per PR) vs local dev (create once, reuse
forever). Without them, `provision.ps1` is idempotent but never cleans up automatically.

**Implementation:** session-scoped autouse fixture in `environments/cloud/conftest.py`
that checks the flag and calls `az group delete` in a `finally` block.

---

### 3. `AgentSettings.read()` / config verification

NEXT_STEPS noted that `ConfigWriter` needs a `read()` method so tests can assert
that written values match expected infra outputs — useful for catching silent
injection failures.

Add to `src/common/infra/config.py`:
```python
class DotEnvWriter:
    @staticmethod
    def read(path: Path) -> AgentSettings: ...

class AppSettingsWriter:
    @staticmethod
    def read(path: Path) -> AgentSettings: ...
```

Unit-testable in `_tests/` with no Azure dependency.

---

### 4. Key Vault references in config files

NEXT_STEPS flagged writing Key Vault references instead of raw secrets for
production-like environments:
```
# instead of: CLIENT_SECRET=abc123
# write:       CLIENT_SECRET=@Microsoft.KeyVault(SecretUri=https://kv-xxx.vault.azure.net/secrets/client-secret/)
```

App Service resolves these at runtime; ACI does not (secrets must be resolved
before container start, which `inject_config.py` already handles via `az keyvault
secret show`). Defer until an App Service environment is added.

---

### 5. README update (post-migration)

Once `environments/` replaces `src/tests/`, the README needs updating:
- New directory layout diagram
- Updated `uv run pytest` examples (with `environments/local`, `environments/cloud`)
- Prerequisites section: add Docker (required for the local/cloud test runner)
- Per-environment setup instructions (local: run `provision.ps1` or `run_local.ps1 -Provision`)

---

## Open questions / next decisions

1. **ACI HTTPS** — ACI doesn't provide TLS out of the box; the Bot Framework
   requires HTTPS for the messaging endpoint. Options:
   - Use an Application Gateway or Front Door in front (more Bicep, slower provision)
   - Use [ngrok sidecar](https://ngrok.com/docs/using-ngrok-with/docker/) in the container group (quick but requires ngrok account)
   - Use a devtunnel forwarded to ACI (same devtunnel tooling as local env)
   - Use App Service only for HTTPS agents, keep ACI for anonymous/internal testing

2. **ACR image naming** — Should image tags include a version/commit hash
   (`quickstart-python:abc1234`) or always overwrite `latest`? Hash tags make
   rollback safe; `latest` simplifies the ACI deployment step.

3. **`DeployedInfra` scope** — Should it also cover Environment 1's Azure Bot
   details (app ID, tenant ID, client secret ref) so both environments load from
   the same file format? Or keep a separate `LocalInfra` for Environment 1?

4. **Port allocation for parallel agents** — `SourceScenario` currently hardcodes
   port 3978. A session-scoped fixture that assigns ports from a pool is needed
   before tests can run in parallel.

5. **`conftest.py` fixture vs `@pytest.mark.agent_test`** — Validate the mark-based
   approach works correctly with the `microsoft_agents.testing` plugin before
   deciding whether to migrate to the fixture pattern.
