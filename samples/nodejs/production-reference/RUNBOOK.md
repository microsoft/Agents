# Operations runbook

## Alerts

Alert on sustained HTTP 5xx responses, readiness failures, authentication rejections above baseline, Blob latency/failures, and process restarts. Alert messages must identify environment and component only; never include user text or tokens.

## Common incidents

| Signal | Likely cause | Action |
|---|---|---|
| `/health/live` fails | Process unavailable | Inspect App Service restart events and deployment state. Roll back if a new deployment caused it. |
| `/health/ready` is `503` | Blob permission, network, or configuration failure | Confirm managed identity role and `BLOB_CONTAINER_URL`; do not add a connection string as a quick fix. |
| `401`/`403` on messages | Web Chat identity or issuer mismatch | Check client ID, tenant, issuer validation, exact audience, and service URL policy. |
| Repeated state conflicts | Concurrent turns for one conversation | Add a bounded, idempotent domain-level retry policy before increasing scale. |

## Data handling

State contains support issue-capture content. Define Blob lifecycle retention, access review, and deletion process before launch. This sample does not store transcripts. Normal telemetry must not include raw user activities, credentials, or token values.
