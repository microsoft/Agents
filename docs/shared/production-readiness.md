# Shared production-readiness contract

These requirements apply to JavaScript/TypeScript, Python, and .NET Agents SDK
agents. Language guides own the API/configuration mapping. The
[readiness matrix](readiness-matrix.md) owns control IDs, applicability,
dependencies, and evidence rules. Plugin matrix copies are packaged with each
skill so an installed language plugin remains self-contained.

## Boundary and evidence

Record channel, cloud, host, inbound audiences/issuers, caller applications,
outbound destinations, data, state, dependencies, scale, retention, and owner.
Reject traffic outside that boundary. Do not broaden it to resolve configuration
errors without agreement on the changed architecture.

Use `verified`, `implemented`, `missing`, `not applicable`, or `blocked` for every
control. Identify the evidence and environment. Source, tests, and Bicep are
implementation evidence; they do not prove that an Azure deployment is safe.

- **Code-hardened:** applicable source controls exist and local checks pass.
- **Deployment-ready:** infrastructure, configuration, smoke tests, rollback,
  and operator documentation exist.
- **Production-verified:** the selected deployed environment passed applicable
  security, recovery, telemetry, alert, traffic-protection, smoke, and rollback checks.

## Required decisions and controls

1. **Authentication and authorization:** verify signature, lifetime, exact
   audience, permitted issuers, tenant and cloud as appropriate for the token
   family. Bot Service channel identity is distinct from end-user identity.
   Trusted-app/agent-to-agent endpoints must authorize verified `azp`/`appid`
   against an application allow list before processing. Decide explicitly whether
   caller authorization applies to a channel; do not apply an end-user allow list
   to the Azure Bot connector.
2. **Outbound traffic:** enable the SDK's opt-in host validator. Its host entries
   also allow subdomains. Add exact channel-host checks when the selected boundary
   is narrower. Compare activity service URLs to verified `serviceurl` claims.
   Reuse the policy with supported token-bearing downloaders. Arbitrary HTTP,
   model, retrieval, and tool clients require their own destination policy and
   network egress protection. Test HTTPS, credentials in URLs, redirects, private
   addresses, and rejected destinations according to the installed SDK's coverage.
3. **Configuration and secrets:** fail startup for absent or unsafe production
   values. Prefer managed/workload identity or certificates. If secrets are
   unavoidable, use a managed secret store, rotation, and least privilege. Separate
   development and production. Scan source and deployment artifacts for secrets.
4. **State:** use durable storage when conversation/user/job state must survive
   turns, restarts, or replicas. Never fall back to memory in production. Record
   schema version, backwards compatibility, retention, deletion, access reviews,
   backup and recovery. Reject stale writes; use bounded domain retries only when
   safe. Do not replay entire turns after a reply or external side effect.
5. **HTTP and lifecycle:** bound payloads including chunked bodies, restrict
   protected routes, use generic errors, bound downstream timeouts/retries, and
   make side effects idempotent. Keep liveness independent of dependencies and
   readiness dependent on required services without disclosing details. Drain
   active work before closing clients and flushing telemetry.
6. **Traffic protection:** configure an authenticated caller-aware edge or another
   trusted control. Connector IP limiting is not end-user rate limiting. The
   references' Bicep does not supply this layer. Preserve Bot Service routing,
   prevent direct-origin bypass, and capture deployed load/limit evidence.
7. **Observability:** collect request/turn counts, failures, latency, authentication
   rejection, dependency availability, and shutdown evidence. Review exported SDK
   attributes before enabling broad instrumentation. Exclude prompts, messages,
   tokens, authorization headers, and unnecessary user/conversation identifiers.
   Set sampling, retention, access controls, alert thresholds, and an on-call owner.
8. **Operations:** exercise deployment, negative security tests, state restart and
   replica recovery, telemetry, alerts, smoke tests, and rollback. Record artifact,
   SDK commit/version, environment, command, result, and owner.

## Conditional capabilities

Apply the matrix's additional controls only to capabilities present:

- Delegated APIs: minimal scopes, consent/error paths, sign-out and token redaction.
- Attachments: source/type/size checks, malware policy, timeout, retention/deletion.
- Proactive/long-running work: durable references/jobs, authenticated delivery,
  idempotency, cancellation/status authorization, and bounded retries.
- Models/retrieval: trusted instruction separation, source/tenant authorization,
  provenance, bounded inputs/outputs/cost, safety policy and evaluations.
- Tools/side effects: typed server validation, allow lists, least privilege,
  authorization, operation-safe retries and audit evidence; obtain explicit
  approval for high-impact operations where the scenario requires it.
- Transcripts: legal basis, disclosure, encryption, access, retention and deletion.

## Reference profile

Each reference captures one summary and impact in conversation state. It does not
create or route support tickets. The profile is Azure public cloud, App Service,
Azure Bot Service Web Chat, user-assigned managed identity and Blob storage.
Attachments, transcripts, models, tools, retrieval, proactive messaging, and
delegated user APIs are not included. Application caller authorization is not
applicable to the selected Bot Service channel path.

Python and .NET consume stable released SDK packages from PyPI and NuGet.
The samples pin the current release versions for repeatable installs. Revalidate
API behavior and production controls when updating those versions.
