<!-- # End-to-End Testing

This folder contains end-to-end integration tests for Microsoft Agents SDK samples across Python, Node.js, and .NET. Tests launch agent processes locally via PowerShell scripts, send activities using the `microsoft-agents-testing` client library, and assert on the responses.

## How it works

Each test scenario targets one agent sample in one SDK language. The framework:

1. Resolves the agent under `src/agents/<scenario>/<language>/run_agent.ps1`
2. Spawns the agent process via PowerShell
3. Waits a configurable delay for the agent to start
4. Sends activities and asserts on responses through `AgentClient`
5. Terminates the agent process when the test completes

Test classes follow the pattern of a shared `Base` class containing the actual test logic, with concrete subclasses decorated with `@pytest.mark.agent_test(<scenario>)` for each SDK variant.

### Directory layout

```
src/
  agents/               # One subdirectory per scenario
    quickstart/
      python/           # .env, requirements.txt, run_agent.ps1, src/
      js/               # .env, package.json, run_agent.ps1, src/
      net/              # appsettings.json, run_agent.ps1, *.csproj
      setup.py          # (reserved for future automated setup)
  common/               # Shared helpers: SourceScenario, SDKVersion, utils
  tests/
    basic/              # Test files (test_quickstart.py, ...)
```

### Configuration

Each agent variant reads credentials from its own environment file:

| Language | Config file |
|---|---|
| Python | `.env` (copy from `env.TEMPLATE`) |
| Node.js | `.env` (copy from `env.TEMPLATE`) |
| .NET | `appsettings.local.json` |

The top-level `env.TEMPLATE` shows the variables required by the test harness itself:

```
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID=
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET=
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID=
```

Copy it to `.env` and fill in your values before running.

## Requirements

- [uv](https://docs.astral.sh/uv/) package manager
- PowerShell (`pwsh` or `powershell`) available on `PATH`
- Runtime prerequisites for each agent language you want to test (Python 3.13+, Node.js, .NET SDK)

## Setup

### Install Python

```bash
uv python install 3.13
```

### Install dependencies

```bash
uv sync
```

### Configure credentials

Copy the top-level template and fill in your Azure app credentials:

```bash
cp env.TEMPLATE .env
# edit .env
```

Also configure the `.env` (or `appsettings.local.json` for .NET) inside each agent variant you intend to test.

## Run tests

Run all tests:

```bash
uv run pytest
```

Run tests for a specific scenario or language:

```bash
uv run pytest -k "Quickstart and Python"
uv run pytest -k "QuickstartNet"
```

Run with verbose output:

```bash
uv run pytest -v
```

## Adding a new scenario

1. Create `src/agents/<scenario>/<language>/run_agent.ps1` that installs deps and starts the agent on `http://localhost:3978/api/messages`.
2. Add `src/agents/<scenario>/<language>/env.TEMPLATE` (or `appsettings.json` for .NET) documenting required config values.
3. Create a test file under `src/tests/<group>/test_<scenario>.py` following the `Base` + per-language subclass pattern used in `test_quickstart.py`.
4. If the scenario needs a custom startup delay, pass `delay=<seconds>` to `create_scenario()`.
 -->
