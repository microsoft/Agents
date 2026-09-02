# Prepare a JavaScript agent for production

This guide explains how to move a JavaScript or TypeScript Microsoft 365 Agents SDK agent toward a bounded, evidence-backed production deployment.

Production readiness is more than secure source code. It includes identity, state, traffic protection, observability, infrastructure, tests, deployment evidence, rollback, and operator procedures.

> [!IMPORTANT]
> This guide is a baseline, not a production certification. Apply your organization's security, privacy, reliability, and compliance requirements. Do not describe an agent as production-ready until its selected environment has passed the required deployment and operator checks.

The companion [Web Chat production reference](../samples/nodejs/production-reference/README.md) demonstrates one supported path: Azure App Service, Azure Bot Service Web Chat, managed identity, and Blob state. It does not demonstrate every Agents SDK feature.

The reference labels itself Tier 3 because it supplies durable state, production identity, health checks, observability, tests, infrastructure as code, deployment guidance, rollback, and an operator runbook. The tier describes supplied evidence. It does not certify a deployed workload.

## Production controls

The following control groups apply to a production deployment. Conditional controls apply only when the listed feature exists.

| Control group | Protects against |
|---|---|
| Deployment boundary | Unsupported channels, callers, data, hosts, and operating assumptions |
| Inbound authentication and authorization | Unauthenticated or authenticated-but-unauthorized callers |
| Service URL and outbound-host validation | Spoofed destinations, confused-deputy attacks, SSRF, and token exfiltration |
| Configuration, identity, and secrets | Unsafe defaults, missing controls, excessive privilege, and credential exposure |
| Durable state and concurrency | State loss, replica inconsistency, and conflicting turns |
| HTTP and traffic protection | Oversized payloads, abuse, unsafe retries, and abrupt termination |
| Observability and operations | Undetected failures, sensitive telemetry, and unsafe recovery |
| AI, retrieval, and tools | Prompt injection, unsafe output, data exposure, and unauthorized side effects |

### Maturity states

Use these states instead of a general “production-ready” claim:

- **Code-hardened:** Applicable source controls exist and local checks pass.
- **Deployment-ready:** Infrastructure, production configuration, smoke tests, rollback, and operator documentation exist.
- **Production-verified:** The deployed environment has passed authentication, authorization, state recovery where applicable, telemetry, alert, traffic-protection, smoke, and rollback checks.

## Prerequisites

- A JavaScript or TypeScript agent built with `@microsoft/agents-hosting` or a related Agents SDK package.
- A selected channel, host, identity model, state provider, and deployment environment.
- Access to the infrastructure, identity, telemetry, and traffic-protection configuration for that environment.
- An operator who owns deployment, incidents, retention, deletion, and rollback.

Use the [production reference sample](../samples/nodejs/production-reference/README.md) for executable code. Use the [production-readiness skill](../agent-plugins/agents-for-js/skills/agents-sdk-to-prod/SKILL.md) when an AI coding agent must assess or harden a selected JavaScript scenario.

## Define the deployment boundary

Before you write code, answer these questions:

- **Channel:** Which chat platforms can users connect through?
- **Cloud and host:** Which cloud and hosting service run the agent?
- **Access:** Which channel services, tenants, and calling applications can access it?
- **Data:** Which data classes flow through the agent?
- **Dependencies:** Which services and outbound hosts must be available?
- **Scale and retention:** How many conversations must it support, and how long does it keep them?
- **Operations:** Who deploys, monitors, supports, and rolls back the deployment?

Reject channels and callers outside this boundary. Do not add broad host or caller wildcards only to make configuration easier.

The production reference uses this boundary:

| Property | Selected value |
|---|---|
| Channel | Azure Bot Service Web Chat only |
| Cloud | Azure public cloud |
| Host | Azure App Service |
| Identity | Single-tenant, user-assigned managed identity |
| Inbound audience | Exact agent Entra application ID |
| Activity service host | `webchat.botframework.com` |
| State | Azure Blob Storage |
| Attachments, tools, retrieval, models, proactive messages | Not included |

## Secure inbound and outbound traffic

An agent receives untrusted traffic and makes outbound, often token-bearing, calls. Protect both directions.

### Validate inbound JWTs

Require JWT authentication on the messaging endpoint and every non-health endpoint that changes data, sends messages, or invokes a downstream API. Keep anonymous access for local development only.

Use `startServer` or `createAgentRequestHandler` when their complete pipeline fits the application. They apply JWT authentication. For route-specific authorization, use an explicit Express pipeline and keep this order:

1. Parse JSON with a bounded payload size.
2. Apply `authorizeJWT`.
3. Authorize the calling application when required.
4. Validate the selected channel and service URL.
5. Process the activity with the adapter.
6. Handle errors after the route.

> [!WARNING]
> Do not apply `authorizeJWT` again when `startServer` or `createAgentRequestHandler` already owns authentication. Use the lower-level adapter pipeline when authorization must run between JWT validation and activity processing.

In production:

- Require the exact application audience.
- Enable `connections__<connection>__settings__validateIssuer=true`.
- Configure the expected tenant and cloud-specific authority or issuer list.
- Fail startup when required authentication settings are missing or unsafe.
- Test missing, malformed, expired, wrong-audience, wrong-issuer, and wrong-tenant tokens in the target environment.

### Authorize calling applications

Authentication proves that a token is valid. It does not always prove that the calling application is allowed.

For agent-to-agent or trusted-application endpoints, compare the verified `req.user.azp` claim for v2 tokens or `req.user.appid` claim for v1 tokens with an explicit application allow list. Reject a missing or unauthorized caller before `adapter.process`.

The caller application ID is not the end-user identity. Record whether caller authorization applies to the selected channel. The Web Chat production reference receives channel traffic through Azure Bot Service and does not apply an application caller allow list.

### Validate activity service URLs

An activity contains a service URL that the adapter can use for token-bearing replies. Treat this URL as untrusted.

- Enable the SDK outbound-host validator in production.
- Allow only hosts required by the deployment boundary.
- Compare the activity service URL with the authenticated token's `serviceurl` claim.
- Keep exact channel-host middleware when the deployment boundary is narrower than the SDK's built-in Microsoft host list.

> [!IMPORTANT]
> Outbound host validation is opt-in. When it is disabled, a `serviceurl` claim mismatch is logged as a warning and the activity can continue. Enable the validator explicitly in production.

The SDK can require `connectionsMap.serviceUrl=*` to select its default named connection. This wildcard is connection selection, not authorization. Pair it with an exact audience and an outbound-host policy.

For a general deployment that uses Microsoft channels and services:

```dotenv
OutboundHostValidator__Enabled=true
OutboundHostValidator__IncludeDefaultMicrosoftHosts=true
```

> [!NOTE]
> The bounded Web Chat production reference intentionally sets `OutboundHostValidator__IncludeDefaultMicrosoftHosts=false` and explicitly allows only `webchat.botframework.com`. Use this stricter configuration only when the deployment lists and tests every required host.
>
> A configured host suffix also allows its subdomains. Use the narrowest suffix that supports the selected channel. Verify sovereign-cloud and custom-channel hosts explicitly.

Use the Microsoft defaults for a general Microsoft channel deployment. Set `OutboundHostValidator__IncludeDefaultMicrosoftHosts=false` only for a narrow, tested deployment that explicitly lists every required host. The built-in Microsoft host suffixes include:

- `botframework.com`
- `smba.trafficmanager.net`, `teams.microsoft.com`, and `teams.microsoft.us`
- `graph.microsoft.com`
- `sharepoint.com`
- `svc.ms`
- `blob.core.windows.net`

Local tools such as Agents Playground can use a loopback service URL without authentication. Keep the validator disabled for local development or explicitly allow the required local host. Never carry a local-host exception into production.

The SDK validator protects activity service URLs and supported downloaders that receive the same policy. It is not a process-wide firewall and does not automatically protect arbitrary `fetch` calls, model clients, retrieval clients, or tool implementations. Apply a typed destination allow list and network egress controls to those paths.

### Protect token-bearing downloads

When the agent accepts attachments:

- Pass the same enabled outbound policy to `AttachmentDownloader` or `TeamsAttachmentDownloader`.
- Validate source, media type, size, and file name before processing.
- Define malware scanning, timeout, storage, retention, and deletion policies.
- Test a disallowed attachment host and a failed or oversized download.

For explicit JavaScript configuration, share one validator instance between the adapter and supported downloaders:

```typescript
import {
  AgentApplication,
  AttachmentDownloader,
  CloudAdapter,
  loadAuthConfigFromEnv,
  OutboundHostValidator
} from '@microsoft/agents-hosting'

const authConfig = loadAuthConfigFromEnv()
const outboundHostValidator = new OutboundHostValidator({
  enabled: true,
  includeDefaultMicrosoftHosts: true,
  hosts: ['<custom-host>']
})

const adapter = new CloudAdapter(
  authConfig,
  undefined,
  undefined,
  undefined,
  outboundHostValidator
)

const agent = new AgentApplication({
  adapter,
  fileDownloaders: [
    new AttachmentDownloader('inputFiles', outboundHostValidator)
  ]
})
```

The production reference excludes attachments, so these controls are not applicable to that sample.

## Validate configuration and protect credentials

- Use deployment environment configuration. Do not commit `.env` files.
- Validate required production values and fail startup when a value is absent or unsafe.
- Prefer managed identity, workload identity, or certificates.
- If a secret is unavoidable, store it in Key Vault, rotate it, and restrict access.
- Use a separate identity and configuration for each environment.
- Apply least privilege. Scope Blob roles, model access, downstream API permissions, and deployment permissions to the required resource and operation.
- Run secret scanning where available.

## Use durable state when state must survive

`MemoryStorage` is for local development. A stateless agent does not need durable state. Use Blob, Cosmos DB, or an equivalent durable provider when conversation, user, authorization, dialog, proactive, or job state must survive a turn, restart, or replica.

- Never silently fall back to memory state in production.
- Define state schema versioning before a schema change.
- Deploy backward-compatible readers before new writers.
- Use bounded, idempotent domain retries for optimistic concurrency conflicts.
- Define retention, access review, deletion, backup, and recovery.
- Verify that state survives restart and replica changes.

## Protect the HTTP host

- Set a JSON payload limit before the message route.
- Keep liveness dependency-independent.
- Make readiness check required dependencies without exposing details.
- Apply rate limits at an authenticated, caller-aware edge such as Front Door or API Management. The Azure Bot connector IP is not an end-user identity.
- Add timeouts, bounded retries, exponential backoff with jitter, and degraded behavior for downstream calls.
- Make externally visible side effects idempotent.
- Send generic client errors. Log only structured, redacted operator diagnostics.
- Drain HTTP traffic and flush telemetry on `SIGTERM` and `SIGINT`.

## Add observability and operator controls

Collect the traces, metrics, and structured logs required to operate the selected scenario. Include request rate, failures, latency, authentication and authorization rejections, state health, outbound-policy rejections, dependency health, and process restarts.

- Initialize telemetry before Agents SDK components run.
- Configure trace, metric, and log pipelines separately as required.
- Add service name, deployment environment, and component attributes.
- Do not record raw activities, user identifiers, prompts, completions, access tokens, cookies, connection strings, or attachment contents by default.
- Set sampling and retention deliberately. Test redaction.
- Alert on readiness failure, elevated 5xx, authentication changes, outbound-policy rejection, dependency failure, and restart loops.
- Maintain deployment, rollback, incident, retention, and deletion procedures.

Use `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` only when a built-in category is not required. Supported values are `STORAGE`, `AUTHENTICATION`, `AUTHORIZATION`, and `DIALOGS`, separated by commas or spaces.

## Add AI controls when the agent invokes a model

AI controls do not replace normal web-service controls.

### Treat model input as untrusted

User messages, retrieved documents, tool responses, and conversation history are data, not trusted instructions.

- Bound input length, history, output tokens, time, retries, and cost.
- Keep security policy and tool constraints in trusted code.
- Do not put secrets or unnecessary personal data in prompts.
- Validate model output against a typed schema or allow list.
- Define input and output safety actions: block, retry, clarify, or escalate.
- Define fallback behavior for invalid output, policy rejection, budget exhaustion, and model outage.

### Protect tools and external actions

- Allow-list each tool and validate typed input on the server.
- Authorize every action with the least-privileged identity.
- Require explicit user confirmation for irreversible or high-impact actions.
- Apply destination allow lists, timeouts, idempotency keys, rate limits, and audit events.
- Never forward inbound authorization headers, cookies, or raw model output to a tool.

### Evaluate before release

Maintain a versioned evaluation set for expected requests, prompt injection, data exfiltration, malformed output, unsafe content, tool misuse, and outages. Run it before model, prompt, deployment, retrieval, or tool changes.

## Apply feature-specific controls

| Feature | Required controls |
|---|---|
| Delegated user APIs | User authorization handler, minimum scopes, sign-out, token redaction, and consent/error tests |
| Proactive messaging | Durable conversation references, authorized caller and delivery path, outbound-host validation, idempotency, and failure handling |
| Long-running work | Durable job state, acknowledgment, authenticated status and cancellation, deadline, bounded retry, and authorized delivery |
| Attachments | Shared outbound policy, source/type/size validation, malware policy, timeout, and retention/deletion |
| Transcripts | Legal basis, disclosure, encryption, access review, content-safe telemetry, retention, and deletion |
| Retrieval | Source authorization, tenant isolation, provenance, injection resistance, bounded content, destination policy, and evaluation |
| Tools and side effects | Tool allow list, typed validation, authorization, destination policy, confirmation, idempotency, and audit evidence |

## Production checklist

- [ ] The channel, cloud, host, identities, callers, data, dependencies, scale, retention, and owner are explicit.
- [ ] Production configuration fails when required identity, issuer, state, telemetry, or outbound-host settings are absent or unsafe.
- [ ] The message endpoint rejects missing, invalid, wrong-audience, wrong-issuer, wrong-tenant, and unauthorized-caller tokens as applicable.
- [ ] The adapter rejects a disallowed service host and a service URL that differs from the authenticated claim.
- [ ] Supported token-bearing downloaders use the same outbound policy.
- [ ] Arbitrary HTTP, model, retrieval, and tool clients have separate destination and network egress controls.
- [ ] Payload limits, safe middleware order, generic failures, probes, and graceful shutdown work.
- [ ] Durable state survives restart; conflict and retention behavior are defined.
- [ ] Telemetry excludes sensitive content and alerts cover required operational signals.
- [ ] Applicable AI, retrieval, tool, attachment, transcript, proactive, and long-running controls pass their tests.
- [ ] Infrastructure validates; deployment smoke, rollback, and operator procedures have been exercised.

## Next steps

- Run the [Web Chat production reference](../samples/nodejs/production-reference/README.md).
- Use the [production-readiness skill](../agent-plugins/agents-for-js/skills/agents-sdk-to-prod/SKILL.md) to assess a selected JavaScript scenario.
- Review [Agents SDK JavaScript authentication](https://learn.microsoft.com/microsoft-365/agents-sdk/azure-bot-authentication-for-javascript).
- Review [Agents SDK storage](https://learn.microsoft.com/microsoft-365/agents-sdk/storage).
- Review the [Agents SDK telemetry package](https://github.com/microsoft/Agents-for-js/tree/main/packages/agents-telemetry).
- Review [managed identities for App Service](https://learn.microsoft.com/azure/app-service/overview-managed-identity).
