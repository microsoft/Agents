# Shared production-reference assets

The [Node.js](../../nodejs/production-reference/README.md),
[Python](../../python/production-reference/README.md), and
[.NET](../../dotnet/production-reference/README.md) samples use the common
`infra/main.bicep` module through their language-specific wrappers.

The module provisions the same App Service, Azure Bot Web Chat, managed identity,
Blob state and Application Insights architecture. Its language parameter selects
runtime, startup command and native configuration. Each deployment uses its own
name/resources and state container.

Use [the common runbook](RUNBOOK.md) for required deployed/operator evidence and
`scripts/smoke-test.py` for probes and missing-JWT rejection. Language-specific
deployment guides describe how to package each runtime. Neither Bicep nor the
smoke script configures/verifies caller-aware traffic protection or alerts.
