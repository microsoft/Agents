# Prepare a Python agent for production

Apply the [shared production controls](../shared/production-readiness.md) and
[readiness matrix](../shared/readiness-matrix.md) to your selected Python agent.
Use the [production reference](../../samples/python/production-reference/README.md)
for executable code and the [production skill](../../agent-plugins/agents-for-python/skills/agents-sdk-to-prod/SKILL.md)
for discovery, assessment, implementation, verification, and handoff.

The reference is bounded to Web Chat, Azure public cloud, App Service, managed
identity, and Blob state. It supplies implementation evidence, not deployment
certification. Preserve its boundary or assess the controls for a different host/channel.

## Released SDK baseline

The reference consumes stable Microsoft Agents PyPI packages `1.8.0`.
Install dependencies with the commands in the sample README. No SDK source clone
or custom package build is required. Revalidate API behavior and production
controls when upgrading the released package baseline.

## Hosting and authentication

The sample owns its `aiohttp` application. Its pipeline is:

1. Catch errors and return generic responses.
2. Parse a body bounded to 256 KiB, including chunked requests.
3. Run SDK `jwt_authorization_middleware` for `/api/messages` only.
4. Restrict the channel to `webchat`, validate an exact HTTPS service host and
   compare a present verified `serviceurl` claim.
5. Run `CloudAdapter.process(request, agent)`.

Anonymous liveness/readiness routes bypass JWT validation. Any additional custom
route that mutates state or calls downstream APIs must get authentication and
authorization explicitly; the reference does not automatically protect new routes.

Read identity from `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID`,
`TENANTID` and `CLIENTSECRET`, matching the other Python samples. The reference
loads `.env` during local startup so the file can select `APP_ENV=development`.
Shell values take precedence; explicit `APP_ENV=production` skips the file.
Without an environment setting in either location, startup defaults to production.

Construct `AgentAuthConfiguration` explicitly: production uses
`AuthTypes.user_managed_identity`, the exact `client_id`, the selected `tenant_id`,
`anonymous_allowed=False`, and `validate_issuer=True`. The reference permits the
Bot Framework issuer `https://api.botframework.com` only. It rejects direct Entra
application callers. If your channel uses a different token family, select its
metadata/issuers, tenant policy and caller authorization deliberately and test
them; do not broaden the reference's issuer list silently.

For trusted application endpoints, authorize the **verified**
`request["claims_identity"].claims["azp"]` or `appid` before adapter processing.
Never make acceptance decisions from decoded but unverified JWT claims. Require
expiration explicitly if the installed validator permits tokens without `exp`.

The SDK's default connection name is `SERVICE_CONNECTION`. Connection-map keys
are `SERVICEURL`, `CONNECTION`, and `AUDIENCE`. The `SERVICEURL='*'` selection rule
does not grant authorization; keep exact audience and destination checks.

## Outbound destinations

Pass an explicit `OutboundHostValidator` to `CloudAdapter`:

```python
validator = OutboundHostValidator(
    enabled=True,
    hosts=["webchat.botframework.com"],
    include_default_microsoft_hosts=False,
)
adapter = CloudAdapter(connection_manager=connections, host_validator=validator)
```

Configured entries are suffix rules. The reference additionally rejects
subdomains, URL credentials, non-HTTPS schemes and non-default production ports.
The Python main validator is not a process-wide firewall or a DNS-rebinding
defense. Add network/destination controls for arbitrary clients. Inspect supported
downloaders before wiring the same validator when adding attachments.

## Durable state and lifecycle

Use `BlobStorageConfig(container_name=..., url=..., credential=...)` with an async
`ManagedIdentityCredential` in production. `url` is the Blob **service** URL; it is
not a container URL. Development uses Azurite through an explicit connection string.

The released SDK Blob provider overwrites unconditionally. The reference's
`VersionedBlobStorage` adds ETag-conditional writes and create-only first writes
to reject conflicting replicas. State is saved before acknowledging success.
Conflicts produce a safe error; the sample does not replay a whole turn. This
wrapper relies on the released provider's protected extension methods; recheck it
when updating the SDK. Test recovery/concurrency against the actual deployed store.

Use `@agent.after_turn` returning `True` to permit normal SDK state saves. The
state records schema `version=1`; incompatible versions fail safely. Deploy
compatible readers before changing writers and preserve compatibility for rollback.

`web.run_app(..., shutdown_timeout=30)` drains requests before application cleanup.
Cleanup closes Blob clients and the identity credential, then shuts down telemetry.
Do not close resources in an early signal handler while turns are active.

## Telemetry and verification

The sample exports only its content-free application spans and metrics. It filters
other instrumentation scopes and disables SDK content-bearing logs. Add reviewed
SDK instrumentation or request latency metrics when your operational boundary
requires them; inspect output with synthetic sensitive content before promotion.

Run `pytest`, `ruff check`, and `ruff format --check`. HTTP tests exercise real SDK
JWT validation using synthetic signing keys; they do not validate Azure metadata,
Bot Service delivery, managed identity, alerts or edge enforcement. Use the
deployment guide and runbook to gather those separate evidence items.
