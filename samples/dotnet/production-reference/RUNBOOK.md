# .NET production reference operations

Follow [the shared runbook](../../shared/production-reference/RUNBOOK.md).
Record SDK package version `1.8.77` and deployment artifact hashes.
Verify the versioned Blob provider after dependency updates.

Check Kestrel request draining and telemetry flush at `ApplicationStopped` before
host/provider disposal. Do not enable verbose framework/SDK logging on live user
traffic; added instrumentation requires redaction review.
