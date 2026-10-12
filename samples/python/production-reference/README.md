# Production reference — Web Chat support issue capture

The security settings listed in `env.TEMPLATE` mirror the Node.js production
profile using Python connection names. They have secure defaults and are checked
at startup in production: managed identity, issuer validation, exact audience,
the default service connection and the Web Chat outbound host policy.
`OTEL_SERVICE_NAME` sets the exported service identity. SDK spans are excluded
entirely; only reviewed application instrumentation is exported.

**Runtime:** Python 3.12 · **Channel:** Web Chat · **Host:** Azure App Service

This reference captures one issue summary and impact in durable conversation
state. It does not create or route tickets. It implements the same bounded flow
as the JavaScript reference with Python-native hosting APIs.

It supplies JWT/issuer/audience validation, exact Web Chat service-host checks,
opt-in SDK outbound validation, managed-identity Blob state, optimistic writes,
256 KiB payload bounds, generic errors, probes, redacted application telemetry,
shutdown cleanup, tests, Bicep and operator guidance. It is not production-certified.

See [the Python guide](../../../docs/python/production-ready-agent.md),
[shared contract](../../../docs/shared/production-readiness.md), and
[production skill](../../../agent-plugins/agents-for-python/skills/agents-sdk-to-prod/SKILL.md).
Attachments, transcripts, delegated APIs, models, retrieval, tools, proactive
messages and other channels are outside this reference's boundary.

## SDK packages and local setup

Install Python 3.12. The sample uses the stable Microsoft Agents PyPI packages
`1.8.0`, the latest released version at the time this reference was updated.
Pip installs the published wheels directly; no SDK checkout or wheel build is needed.

```powershell
python -m venv .venv
.venv/Scripts/python -m pip install --upgrade pip
.venv/Scripts/python -m pip install -r requirements-dev.txt
.venv/Scripts/python -m pytest -q
.venv/Scripts/python -m ruff check src tests scripts
.venv/Scripts/python -m ruff format --check src tests scripts
```

On Linux/macOS replace `.venv/Scripts/python` with `.venv/bin/python`.

Run Azurite separately, then create the local environment file:

```powershell
Copy-Item env.TEMPLATE .env
.venv/Scripts/python -m src.main
```

Use Agents Playground against `http://localhost:3978/api/messages`. The application
loads `.env` for local startup, including its `APP_ENV=development` setting.
Existing shell variables take precedence; an explicit production environment
skips `.env`. `env.TEMPLATE` lists optional local credentials.
Anonymous/loopback access is confined to explicit development mode.

## Production and verification

`APP_ENV` defaults to `production`. Startup requires GUID identity values,
an HTTPS Azure Blob service URL and telemetry configuration. Production rejects
client secrets and storage connection strings. Only the user-assigned managed
identity is used for production Blob access and channel replies.

The released SDK provider does not check ETags; `src/storage.py` adds conditional
updates and create-only writes. Conflicts fail safely without replaying turns.
The application saves state before acknowledging success. Local tests include
synthetic JWT validation and HTTP rejection/probe checks; deployed recovery,
RBAC, telemetry, edge, alert and rollback evidence remain required.

See [deployment](DEPLOYMENT.md) and [operations](RUNBOOK.md). Application spans
and counters are exported without user content; broad SDK/log auto-export is
intentionally filtered until its attributes are reviewed for the selected workload.
