# Environments

Each subdirectory is a self-contained test environment. Environments share the same
agent scenarios and test logic (`src/common/`) but differ in how agents are deployed
and how the test runner reaches them.

| Environment | Status | Agent deployment | When to use |
|---|---|---|---|
| [`local/`](local/) | ✅ Active | Subprocesses inside a single Docker container | Day-to-day development, CI on a VM runner |
| `cloud/` | ⬜ Planned | Separate ACI containers, addressable by URL | Integration testing against live Azure infra |

## How environments relate

- **Shared test logic** — `environments/local/tests/` and `environments/cloud/tests/`
  contain identical test classes (both subclass `BaseTest*` from `src/common/`). The
  only difference is which `conftest.py` wires up the `scenario` fixture.
- **Shared agent source** — Agent code lives under `environments/local/agents/` today.
  Cloud Dockerfiles will `COPY` from the same source tree.
- **Independent infra** — Each environment has its own `infra/` (Bicep), `scripts/`,
  and `azure.yaml`. Provisioning one environment has no effect on the other.

## Running tests

```powershell
# Local environment (Docker, all languages):
./environments/local/scripts/run_local.ps1

# Specific environment via pytest directly:
uv run pytest environments/local/
uv run pytest environments/cloud/   # requires infra-outputs.json
```
