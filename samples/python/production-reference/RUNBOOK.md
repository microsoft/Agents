# Python production reference operations

Follow [the shared runbook](../../shared/production-reference/RUNBOOK.md).
Record SDK package version `1.8.0` and deployment artifact hashes.
Verify the versioned Blob provider after dependency updates.

Check that App Service shutdown drains aiohttp before Blob/credential cleanup
and telemetry shutdown. The application does not export SDK logs or unrestricted
instrumentation scopes. Review any added instrumentation before enabling export.
