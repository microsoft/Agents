---
title: Prepare an agent for production
description: "Prepare a Microsoft 365 Agents SDK agent for production with authentication, outbound controls, durable state, observability, and deployment verification."
ms.date: 10/09/2026
ms.topic: how-to-guide
author: <GitHub-username>
ms.author: <Microsoft-alias>
manager: kjette
ms.reviewer: cyanderson
ms.service: microsoft-365-agents-sdk
zone_pivot_groups: dev-lang-python-nodejs-dotnet
zone_pivot_group_filename: agents-sdk/zone-pivot-groups.json
---
# Prepare an agent for production

Prepare your Microsoft 365 Agents SDK agent for production by defining its deployment boundary, securing traffic, protecting state, and verifying its behavior in the target environment. This article is for developers and operators moving an existing agent from local development to a supported deployment.

The production references discussed here capture an issue summary and impact in conversation state. They use Azure Bot Service Web Chat, Azure App Service, user-assigned managed identity, and Azure Blob Storage in Azure public cloud. They don't create support tickets or invoke models, retrieval, tools, delegated APIs, attachments, transcripts, or proactive messages.

> [!IMPORTANT]
> Reference code and passing local tests don't certify a deployment. Apply your organization's security, privacy, reliability, and compliance requirements. Complete the applicable security, recovery, traffic-protection, alert, smoke, and rollback checks in your selected environment before describing it as production-verified.

## Prerequisites

- An existing Microsoft 365 Agents SDK agent.
- A selected channel, cloud, host, identity model, and state provider.
- Access to configure identity, infrastructure, telemetry, and traffic protection for the target environment.
- An operator responsible for deployment, incidents, data retention, deletion, and rollback.

:::zone pivot="python"

The Python production reference uses Python 3.12 and Microsoft Agents packages pinned to `1.8.0`. Install the published dependencies from the reference's requirements files. An SDK source build isn't required.

Use the [Python production reference README](https://github.com/microsoft/Agents/blob/main/samples/python/production-reference/README.md) for executable code and dependency installation. The [Python production-readiness skill](https://github.com/microsoft/Agents/blob/main/agent-plugins/agents-for-python/skills/agents-sdk-to-prod/SKILL.md) guides an AI coding agent through discovery, assessment, implementation, verification, and handoff.

:::zone-end

:::zone pivot="nodejs"

The Node.js production reference uses Node.js 24 and JavaScript/TypeScript. Its package manifest declares `@microsoft/agents-hosting` and `@microsoft/agents-hosting-storage-blob` dependencies as `^1.8.1`. Use `npm ci` with the supplied lockfile for a repeatable install.

Start with an agent built with `@microsoft/agents-hosting` or a related Agents SDK package. Use the [Node.js production reference README](https://github.com/microsoft/Agents/blob/main/samples/nodejs/production-reference/README.md) for executable code and setup. The [JavaScript production-readiness skill](https://github.com/microsoft/Agents/blob/main/agent-plugins/agents-for-js/skills/agents-sdk-to-prod/SKILL.md) guides an AI coding agent through assessment and hardening of a selected scenario.

The reference's Tier 3 label describes its supplied durable state, production identity, probes, observability, tests, infrastructure as code, deployment guidance, rollback, and operator runbook. It doesn't certify a deployed workload.

:::zone-end

:::zone pivot="dotnet"

The .NET production reference uses .NET 10 and Microsoft Agents NuGet packages pinned to `1.8.77`. Restore the published packages with the reference's `NuGet.Config`. An SDK source build isn't required.

Use the [.NET production reference README](https://github.com/microsoft/Agents/blob/main/samples/dotnet/production-reference/README.md) for executable code and dependency installation. The [.NET production-readiness skill](https://github.com/microsoft/Agents/blob/main/agent-plugins/agents-for-net/skills/agents-sdk-to-prod/SKILL.md) guides an AI coding agent through assessment, implementation, verification, and handoff.

:::zone-end

These versions describe the reference baselines. Revalidate APIs and production controls whenever you upgrade dependencies.

The following control groups apply across all three languages. Conditional controls apply only when the corresponding capability exists.

| Control group | Protects against |
|---|---|
| Deployment boundary | Unsupported channels, callers, data, hosts, and operating assumptions. |
| Inbound authentication and authorization | Unauthenticated or authenticated-but-unauthorized callers. |
| Service URL and outbound-host validation | Spoofed destinations, confused-deputy attacks, SSRF, and token exfiltration. |
| Configuration, identity, and secrets | Unsafe defaults, missing controls, excessive privilege, and credential exposure. |
| Durable state and concurrency | State loss, replica inconsistency, and conflicting turns. |
| HTTP and traffic protection | Oversized payloads, abuse, unsafe retries, and abrupt termination. |
| Observability and operations | Undetected failures, sensitive telemetry, and unsafe recovery. |
| AI, retrieval, and tools | Prompt injection, unsafe output, data exposure, and unauthorized side effects. |

Use the [shared production-readiness contract](https://github.com/microsoft/Agents/blob/main/docs/shared/production-readiness.md) and [readiness matrix](https://github.com/microsoft/Agents/blob/main/docs/shared/readiness-matrix.md) to compare the language implementations and record control IDs, applicability, dependencies, and evidence. The procedures and release checks below apply the shared requirements within this article.

## 1. Define the deployment boundary

Record the supported channels, cloud, host, inbound audiences and issuers, tenants, calling applications, outbound destinations, data classes, dependencies, scale, retention, and operating owner. Reject requests outside that boundary.

The references use the following profile:

| Property | Selected value |
|---|---|
| Channel | Azure Bot Service Web Chat |
| Cloud and host | Azure public cloud and Azure App Service |
| Identity | Single-tenant, user-assigned managed identity |
| Inbound audience | Exact agent client ID |
| Activity service host | `webchat.botframework.com` |
| State | Azure Blob Storage |
| Application caller allow list | Not applicable to the selected Bot Service channel path |

Changing the channel, token family, host, or capabilities requires reassessing this profile. Don't broaden an issuer or destination allow list just to bypass a configuration error.

Track each control with one of these statuses. Record the evidence, environment, and owner.

| Status | Meaning |
|---|---|
| Verified | The control passed the required check in the selected environment, and the evidence is identified. |
| Implemented | Source, configuration, infrastructure, tests, or documentation exist, but required verification evidence is absent. |
| Missing | The control applies and no sufficient implementation exists. |
| Not applicable | The capability or risk doesn't exist within the deployment boundary; record why. |
| Blocked | A named decision, dependency, permission, or environment prevents progress. |

Use these maturity states to report progress:

| State | Required evidence |
|---|---|
| Code-hardened | Applicable source controls exist and local checks pass. |
| Deployment-ready | Infrastructure, production configuration, smoke tests, rollback, and operator documentation exist. |
| Production-verified | The target deployment passes its applicable security and operational checks. |

## 2. Authenticate and authorize incoming requests

Protect the messaging endpoint and every custom endpoint that changes state, sends messages, or calls a downstream API. Keep anonymous access limited to health probes and explicitly selected local development paths.

Validate token signatures, signing keys, expiration, exact audience, and permitted issuers. Apply the tenant and cloud policy appropriate to the selected token family. Never authorize a request from decoded but unverified claims.

Bot Service channel identity, calling-application identity, and end-user identity are different authorization concerns. For trusted-application or agent-to-agent endpoints, compare the verified `azp` or `appid` claim with an application allow list before processing the activity. Calling a delegated user API also requires user authorization and the appropriate scopes.

:::zone pivot="python"

### Configure the Python request pipeline

The reference owns its `aiohttp` application and applies this order:

1. Catch errors and return generic responses.
2. Read a JSON body bounded to 256 KiB, including chunked requests.
3. Run `jwt_authorization_middleware` for `/api/messages`.
4. Check the verified issuer and expiration, restrict `channelId` to `webchat`, validate the service URL, and compare a present verified `serviceurl` claim.
5. Call `CloudAdapter.process(request, agent)`.

Construct `AgentAuthConfiguration` explicitly with `AuthTypes.user_managed_identity`, the exact `client_id`, the selected `tenant_id`, `anonymous_allowed=False`, and `validate_issuer=True`. The reference additionally restricts the verified issuer to `https://api.botframework.com` and requires an unexpired `exp` claim. Require expiration explicitly if the installed validator permits tokens without `exp`. This profile rejects direct Microsoft Entra application callers.

For a separate trusted-application profile, authorize `request["claims_identity"].claims["azp"]` or `appid` only after validation. Select that profile's metadata, issuers, and tenant policy explicitly.

The middleware protects `/api/messages`; adding a custom route doesn't automatically protect it. Add authentication and authorization to each new protected route.

:::zone-end

:::zone pivot="nodejs"

### Configure the Node.js request pipeline

Use `startServer` or `createAgentRequestHandler` when their complete hosting pipeline fits your application. Those helpers apply JWT authentication. Don't apply `authorizeJWT` a second time around a helper that already owns authentication.

The reference owns an Express host so it can apply this order:

1. Parse JSON with a 256 KiB payload limit.
2. Apply `authorizeJWT`.
3. Enforce the trusted issuer policy, including the reference's verified-claim fallback when native issuer validation isn't enabled.
4. Validate the Web Chat service host.
5. Call `adapter.process`.
6. Handle application errors after the route.

Require the exact audience, the expected tenant, and `connections__serviceConnection__settings__validateIssuer=true`. The general setting pattern is `connections__<connection>__settings__validateIssuer=true`; use your selected connection name. Check the effective issuer behavior of the installed SDK, not just the presence of the configuration setting.

For trusted-application endpoints, use an explicit Express pipeline and insert authorization based on verified `req.user.azp` for v2 tokens or `req.user.appid` for v1 tokens between JWT validation and adapter processing. Reject a missing or unauthorized caller before `adapter.process`, and validate the selected channel and service URL. Configure the cloud-specific authority or issuer list for that profile. The reference's Bot Service Web Chat path doesn't apply an application caller allow list.

:::zone-end

:::zone pivot="dotnet"

### Configure the .NET request pipeline

Register agent services with `AddAgentDefaults().AddAgent<SupportAgent>()`, and configure ASP.NET Core authentication separately with `AddJwtBearer` and `AddAuthorization`. Registering an adapter alone doesn't secure a route.

The reference requires signed tokens, signing-key validation, expiration and lifetime validation, the exact audience, and the Bot Service issuer `https://api.botframework.com`. It uses HTTPS OpenID metadata, disables authentication error details, and sets `MapInboundClaims=false` to preserve original claim names. Direct Microsoft Entra application callers are outside this profile.

Apply generic error handling, authentication, and authorization before the bounded activity read and channel/service-URL checks. Protect `/api/messages` with `RequireAuthorization()` in production, then call `IAgentHttpAdapter.ProcessAsync`. The 256 KiB read also bounds chunked bodies. Health endpoints remain anonymous.

For trusted-application scenarios, select the appropriate cloud and tenant metadata and issuers, bind the tenant to verified claims, and authorize `azp` or `appid` before adapter processing. Reassess the Bot Service issuer profile if the channel changes its token family.

:::zone-end

## 3. Restrict outbound destinations

An activity's service URL can become the destination of a token-bearing reply. Enable the SDK outbound-host validator explicitly and allow only destinations required by the deployment. Compare the activity URL with the verified token's `serviceurl` claim when present, and define whether the selected token profile requires that claim.

Configured host entries also match subdomains. Add exact-host checks when the deployment permits only one host. Test HTTPS enforcement, URL credentials, ports, redirects, private addresses, and destination rejection according to the call path's actual coverage.

A connection-map service URL of `*` selects a connection. It doesn't authorize a destination or replace exact-audience validation.

:::zone pivot="python"

Pass an explicit policy to the adapter. With `connections` set to the application's connection manager, the reference uses:

```python
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import OutboundHostValidator

validator = OutboundHostValidator(
    enabled=True,
    hosts=["webchat.botframework.com"],
    include_default_microsoft_hosts=False,
)
adapter = CloudAdapter(
    connection_manager=connections,
    host_validator=validator,
)
```

The reference's HTTP boundary additionally rejects subdomains, non-HTTPS URLs, URL credentials, query strings, fragments, and non-default production ports. Don't treat the Python host validator as a DNS-rebinding defense.

:::zone-end

:::zone pivot="nodejs"

The bounded Web Chat reference requires these environment settings:

| Setting | Value |
|---|---|
| `OutboundHostValidator__Enabled` | `true` |
| `OutboundHostValidator__IncludeDefaultMicrosoftHosts` | `false` |
| `OutboundHostValidator__Hosts` | `webchat.botframework.com` |

The reference relies on the enabled outbound validator for SDK service-URL-claim mismatch enforcement. Without that validator or another explicitly enabled SDK enforcement option, a mismatch can be logged as a warning and processing can continue. Keep the exact-host middleware as well. That middleware checks the hostname; don't assume it independently validates every URL component. Verify the combined middleware and SDK behavior, including whether claim comparison checks the hostname or the complete URL, for the installed version.

The SDK can require `connectionsMap.serviceUrl=*` to select its default named connection. Keep the exact audience and outbound-host policy alongside that selection rule.

For a general deployment that uses Microsoft channels and services, enable the validator and the Microsoft defaults:

| Setting | Value |
|---|---|
| `OutboundHostValidator__Enabled` | `true` |
| `OutboundHostValidator__IncludeDefaultMicrosoftHosts` | `true` |

The built-in Microsoft host suffixes include `botframework.com`, `smba.trafficmanager.net`, `teams.microsoft.com`, `teams.microsoft.us`, `graph.microsoft.com`, `sharepoint.com`, `svc.ms`, and `blob.core.windows.net`. Verify the installed SDK's list and sovereign-cloud or custom-channel hosts explicitly. The bounded Web Chat reference intentionally uses `OutboundHostValidator__IncludeDefaultMicrosoftHosts=false` and permits only its tested host.

Local tools such as Agents Playground can use an unauthenticated loopback service URL. For local development, explicitly allow the required local host or disable the validator only in that development environment. Never carry either exception into production.

When adding attachments, pass the same enabled `OutboundHostValidator` instance to the adapter and supported `AttachmentDownloader` or `TeamsAttachmentDownloader` instances.

This JavaScript example shares a policy between the adapter and a supported downloader for a general Microsoft-channel deployment. Replace `<custom-host>` with an approved additional host, or remove the `hosts` entry if none is needed. It extends beyond the reference's Web Chat-only boundary:

```javascript
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

The production reference excludes attachments. Apply the attachment controls in section 8 before enabling this capability.

:::zone-end

:::zone pivot="dotnet"

Register `IOutboundHostValidator` explicitly. The reference creates its validator with these options:

```csharp
new OutboundHostValidator(new OutboundHostValidatorOptions
{
    Enabled = true,
    IncludeDefaultMicrosoftHosts = false,
    Hosts = ["webchat.botframework.com"],
});
```

The HTTP boundary additionally requires the exact Web Chat host over HTTPS on port 443 and rejects credentials, query strings, and fragments. Inspect the validator's synchronous and asynchronous call paths before relying on DNS address checks.

:::zone-end

Disabling the default Microsoft host list is appropriate only for a narrow deployment that lists and tests every required host. Reassess the policy for other channels or clouds. Keep development loopback exceptions out of production.

The SDK validator isn't a process-wide firewall. Arbitrary HTTP calls, such as JavaScript `fetch` or .NET `HttpClient`, and model, retrieval, and tool clients need their own typed destination allow lists and network egress controls. Inspect downloader support before assuming it shares the adapter's policy.

## 4. Validate configuration and protect credentials

Fail startup when required production identity, audience, issuer, storage, telemetry, or destination settings are absent or unsafe. Use separate identities and configuration for each environment.

Prefer managed identity, workload identity, or certificates. If a secret is necessary, use a managed secret store such as Azure Key Vault, restrict access, and rotate it. Don't commit local environment files or credentials. Scan source and deployment artifacts for secrets. Scope storage roles, model access, downstream API permissions, and deployment access to the required operations and resources.

:::zone pivot="python"

The reference reads connection settings under `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__`, including `CLIENTID`, `TENANTID`, and development-only `CLIENTSECRET`. For example, the full client ID key is `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID`, matching the other Python samples. The SDK default connection name is `SERVICE_CONNECTION`; connection-map keys are `SERVICEURL`, `CONNECTION`, and `AUDIENCE`. The `SERVICEURL='*'` selection rule doesn't grant authorization.

`APP_ENV` defaults to production. During local startup, `.env` can explicitly select `APP_ENV=development`; shell values take precedence. An explicit shell setting of `APP_ENV=production` skips `.env`. Production rejects client secrets and storage connection strings and uses the selected user-assigned managed identity.

:::zone-end

:::zone pivot="nodejs"

Set `NODE_ENV=production` explicitly for the reference deployment. Startup validates the service connection, exact connection-map audience, issuer-validation setting, Blob container URL, telemetry connection string, and outbound-host policy.

The connection settings use the `connections__serviceConnection__settings__` prefix. The reference selects `UserManagedIdentity` for channel replies. Its Blob client uses `DefaultAzureCredential` with `managedIdentityClientId`; verify the effective identity and credential configuration in the deployed host.

:::zone-end

:::zone pivot="dotnet"

The reference uses standard ASP.NET configuration under `Connections:ServiceConnection:Settings`, validates the client and tenant IDs, and uses `Microsoft.Agents.Authentication.Msal.MsalAuth` and `UserManagedIdentity`. Production rejects client secrets and storage connection strings. `ConnectionsMap:0:ServiceUrl='*'` selects the connection; it isn't a destination allow list.

Optional `TokenValidation:Audiences` and `TokenValidation:TenantId` must match the service connection; omitted values are derived from it. Use .NET user secrets for local credentials and App Service settings for production overrides.

:::zone-end

## 5. Preserve state and handle concurrent writes

Use durable storage when conversation, user, authorization, dialog, proactive, or job state must survive turns, restarts, or replicas. A stateless agent doesn't require a durable state store. `MemoryStorage` is for local development; don't silently fall back to memory storage in production. Select Blob, Cosmos DB, or an equivalent durable provider supported by your SDK and deployment. For provider configuration, see [Use storage in your agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage).

Define schema versions, retention, deletion, access reviews, backup, and recovery. Deploy compatible readers before changing writers, and preserve compatibility for rollback. Reject stale writes and retry only bounded, idempotent domain operations. Don't replay an entire turn after sending a reply or performing an external action.

Configure retention and deletion independently of the application's conversation lifecycle. Ending a conversation doesn't establish that its persisted data has been deleted.

:::zone pivot="python"

Use `BlobStorageConfig(container_name=..., url=..., credential=...)` with an asynchronous `ManagedIdentityCredential`. The `url` is the Blob service URL, without a container path. Development uses an explicit Azurite connection string.

For the reference's package baseline, the SDK Blob provider writes unconditionally. The sample's `VersionedBlobStorage` adds ETag-conditional updates and create-only first writes. It relies on protected provider extension methods, so recheck it when upgrading the SDK.

The reference saves state before acknowledging success, reports conflicts safely, and doesn't replay a turn. Its `@agent.after_turn` handler returns `True` to permit normal SDK state saves. State uses schema `version=1`, and incompatible versions fail safely.

:::zone-end

:::zone pivot="nodejs"

The reference uses `BlobsStorage`. `BLOB_CONTAINER_URL` includes the container path, while local development uses an Azurite connection string. Blob ETag conflicts surface to the application; define a bounded domain retry policy if your workflow needs one.

Verify that the actual state objects preserve the provider's concurrency information. Test stale writes and restart/replica recovery against the selected store before promotion.

:::zone-end

:::zone pivot="dotnet"

The reference registers `IStorage` with a sample-defined `VersionedStorage`, backed by `BlobContainerClient` and an explicit user-assigned `ManagedIdentityCredential`. Combine the configured Blob service URL and container name to construct the container URI. Development uses Azurite.

At this package baseline, SDK `AgentState` dictionary snapshots don't carry `IStoreItem` ETags. The wrapper carries a Blob version in each turn's state dictionary, removes that metadata from persisted content, and performs conditional creates and updates.

Preserve those version tokens. The reference saves once per changed turn before acknowledging success; repeated changed saves in a turn require reloading the current Blob version. State uses schema `Version=1`.

:::zone-end

## 6. Protect the host and drain active work

Bound payload sizes, including chunked requests, before activity processing. Return generic client errors and use structured, redacted operator diagnostics. Set downstream timeouts and bounded retries with exponential backoff and jitter, and define degraded behavior when dependencies fail. Make externally visible side effects idempotent.

Keep liveness independent of dependencies. Make readiness test required dependencies without exposing connection details or exception bodies. The references expose `/health/live` and `/health/ready`; readiness exercises storage writes and deletes.

Apply rate limits at an authenticated, caller-aware edge, such as Azure Front Door or API Management, or another trusted control. Azure Bot connector IP addresses aren't end-user identities. The reference Bicep doesn't provision this traffic-protection layer. Preserve Bot Service routing, prevent direct-origin bypass, and capture deployed load and limit evidence.

:::zone pivot="python"

`web.run_app(..., shutdown_timeout=30)` drains requests before application cleanup. Cleanup closes Blob clients and the identity credential, then shuts down telemetry. Don't close those resources in an early signal handler while turns are active.

:::zone-end

:::zone pivot="nodejs"

Handle `SIGTERM` and `SIGINT` by closing the HTTP server before shutting down telemetry. Verify that the deployed host allows active requests to drain within its termination deadline.

:::zone-end

:::zone pivot="dotnet"

ASP.NET hosting uses a bounded shutdown timeout. The reference flushes telemetry at `ApplicationStopped`, after Kestrel drains and before provider disposal. Keep disposable resources owned by the host where supported.

:::zone-end

## 7. Configure observability and operations

Collect request and turn counts, failures, latency, authentication and authorization rejections, outbound-policy rejections, dependency health, state health, and process restarts. Configure trace, metric, and log pipelines as needed; one configured pipeline doesn't imply that all signals are exported. Add service name, deployment environment, and component attributes so operators can identify the affected service without recording user content.

Exclude raw activities, messages, prompts, completions, tokens, authorization headers, cookies, connection strings, exception bodies, attachment content, and unnecessary user or conversation identifiers. Don't record user identifiers by default. Inspect telemetry using synthetic sensitive content before promotion. Set sampling, retention, access controls, alert thresholds, and an on-call owner.

:::zone pivot="python"

The reference exports reviewed application spans and metrics, filters other instrumentation scopes, and suppresses SDK content-bearing logs. Add reviewed SDK instrumentation or request latency metrics when your operating boundary requires them. Review attributes before adding broader SDK instrumentation. `OTEL_SERVICE_NAME` sets the service identity;

:::zone-end

:::zone pivot="nodejs"

The start commands preload `telemetry.js` so Azure Monitor initializes before SDK components. The reference includes SDK instrumentation and application spans and counters. Review the complete exported content for the chosen deployment.

:::zone-end

:::zone pivot="dotnet"

The reference exports its application instrumentation source and bounded counters. `OnTurnError` returns a generic response and records a bounded failure category. Keep adapter stack-trace emission disabled, and review SDK attributes and logs before enabling broader export. `OTEL_SERVICE_NAME` sets the service identity;

:::zone-end

Alert on readiness failures, elevated server errors, authentication changes, destination rejections, dependency failures, and restart loops. Maintain deployment, rollback, incident, retention, and deletion procedures, and exercise them with the operating team.

## 8. Apply controls for additional capabilities

The references exclude the following capabilities. Apply their controls when your agent adds them.

| Capability | Additional controls |
|---|---|
| Delegated user APIs | User authorization handler, minimum scopes, sign-out, consent/error tests, and token redaction. |
| Attachments | Shared outbound policy for supported downloaders, source/media-type/size/file-name checks, malware scanning policy, timeout, storage, retention, and deletion. |
| Proactive messaging | Durable protected conversation references, authorized caller and delivery path, outbound validation, idempotency, and failure handling. |
| Long-running work | Durable jobs, acknowledgment, authenticated status and cancellation, deadlines, bounded retries, idempotency, and authorized delivery. |
| Transcripts | Legal basis, disclosure, encryption, access review, content-safe telemetry, retention, and deletion. |
| Model calls | Trusted instruction separation, bounded input/history/output/cost, safety policy, typed output validation, timeouts, bounded retries, and fallback behavior. |
| Retrieval | Source authorization, tenant isolation, provenance, bounded content, destination policy, injection resistance, and evaluation. |
| Tools and side effects | Tool allow lists, typed server validation, least privilege, action authorization, destination controls, timeouts, rate limits, idempotency keys, audit evidence, and explicit user confirmation for irreversible or high-impact actions. |

For attachments, test a disallowed download host and failed or oversized downloads before release. Proactive messaging and long-running work require durable references or job state, or an equivalent durable mechanism supplied by the chosen provider.

### Treat model input as untrusted

AI controls don't replace normal web-service controls. Treat user messages, retrieved documents, tool responses, and conversation history as untrusted data.

- Bound input length, history, output tokens, execution time, retries, and cost.
- Keep security policy and tool constraints in trusted code.
- Don't put secrets or unnecessary personal data in prompts.
- Validate model output against a typed schema or allow list.
- Define input and output safety actions: block, retry, clarify, or escalate.
- Define fallback behavior for invalid output, policy rejection, budget exhaustion, and model outage.

### Protect tools and external actions

- Allow-list each tool and validate typed input on the server.
- Authorize every action with the least-privileged identity.
- Require explicit user confirmation for irreversible or high-impact actions.
- Apply destination allow lists, timeouts, idempotency keys, rate limits, and audit events. Retry only when safe for the operation.
- Never forward inbound authorization headers, cookies, or raw model output to a tool. Validate and authorize an action before executing it.

### Evaluate before release

Maintain a versioned evaluation set covering expected requests, prompt injection, exfiltration, malformed output, unsafe content, tool misuse, and outages. Run it before model, prompt, deployment, retrieval, or tool changes. Apply both model and tool controls to model-driven tools, side-effect controls to tools that change external state, and both retrieval and model controls when retrieved content becomes model input.

## 9. Verify the target deployment

First run the reference's local checks from its sample directory, after installing its dependencies.

:::zone pivot="python"

```powershell
python -m pytest -q
python -m ruff check src tests scripts
python -m ruff format --check src tests scripts
```

Use the Python interpreter from the reference's virtual environment. HTTP tests exercise SDK JWT validation with synthetic signing keys. They don't verify Azure metadata, Bot Service delivery, managed identity, or deployed infrastructure.

:::zone-end

:::zone pivot="nodejs"

```powershell
npm ci
npm test
az bicep build --file infra/main.bicep
```

`npm test` builds the sample and checks its flow, configuration, probes, payload limit, and HTTP authentication boundary. Bicep compilation requires Azure CLI and doesn't verify a deployed environment.

:::zone-end

:::zone pivot="dotnet"

```powershell
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet test tests/ProductionReference.Tests.csproj -p:RestoreConfigFile=NuGet.Config
dotnet format ProductionReference.csproj --verify-no-changes --no-restore
dotnet format tests/ProductionReference.Tests.csproj --verify-no-changes --no-restore
```

Use the documented .NET SDK and restore properties. The formatting checks reuse the restored project assets and fail if formatting changes would be required.

TestServer checks validate JWT signatures, issuer, audience, and lifetime with synthetic keys. For local Blob recovery and concurrency checks, run Azurite and set `AZURITE_CONNECTION_STRING=UseDevelopmentStorage=true`. These tests don't verify cloud metadata, managed identity, or Bot Service delivery.

:::zone-end

For each applicable deployed check, record the artifact, SDK version, environment, command or procedure, result, and owner:

- [ ] The channel, cloud, host, identities, callers, data, dependencies, scale, retention, and owner are explicit.
- [ ] Negative configuration tests confirm startup fails for missing or unsafe identity, issuer, state, telemetry, or outbound-host settings.
- [ ] Missing, malformed, unsigned, expired, wrong-audience, wrong-issuer, and wrong-tenant tokens are rejected as applicable to the selected token profile.
- [ ] Trusted-application endpoints reject unauthorized and missing caller identities.
- [ ] Disallowed destinations and mismatched verified service-URL claims are rejected.
- [ ] Supported token-bearing downloaders share the outbound policy; arbitrary HTTP, model, retrieval, and tool clients have separate destination and network egress controls.
- [ ] Custom routes are protected, oversized bodies fail safely, and downstream failures don't disclose sensitive details.
- [ ] State survives restarts and replica changes; stale writes fail safely; retention and deletion work.
- [ ] Managed identity and resource permissions work with the intended least privilege.
- [ ] Probes, traffic limits, origin restrictions, and graceful termination work on the supported host.
- [ ] Telemetry excludes sensitive content and required alerts reach the operator.
- [ ] Applicable model, retrieval, tool, attachment, transcript, proactive, and long-running controls pass their checks.
- [ ] Infrastructure validates, and deployment smoke tests, rollback, and operator procedures have been exercised with compatible state schemas.

Report remaining controls and their evidence status before promotion. Local source and test evidence can establish that a control is implemented; deployed evidence establishes that it works in the selected environment.

Local checks don't prove cloud metadata, managed identity, Bot Service delivery, Azure state recovery, telemetry redaction, alert delivery, traffic protection, or rollback. Use the reference's deployment guide and runbook to gather those separate evidence items.

:::zone pivot="python"

Follow the [Python deployment guide](https://github.com/microsoft/Agents/blob/main/samples/python/production-reference/DEPLOYMENT.md) and [Python operator runbook](https://github.com/microsoft/Agents/blob/main/samples/python/production-reference/RUNBOOK.md).

:::zone-end

:::zone pivot="nodejs"

Follow the [Node.js deployment guide](https://github.com/microsoft/Agents/blob/main/samples/nodejs/production-reference/DEPLOYMENT.md) and [Node.js operator runbook](https://github.com/microsoft/Agents/blob/main/samples/nodejs/production-reference/RUNBOOK.md).

:::zone-end

:::zone pivot="dotnet"

Follow the [.NET deployment guide](https://github.com/microsoft/Agents/blob/main/samples/dotnet/production-reference/DEPLOYMENT.md) and [.NET operator runbook](https://github.com/microsoft/Agents/blob/main/samples/dotnet/production-reference/RUNBOOK.md).

:::zone-end

## Next steps

- Review [authentication configuration](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/configure-authentication-msal).
- Review [storage options](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage).
- Review [managed identities for App Service](https://learn.microsoft.com/en-us/azure/app-service/overview-managed-identity).
- Explore the [Microsoft 365 Agents SDK samples](https://github.com/microsoft/Agents).

:::zone pivot="nodejs"

- Review the [Agents SDK telemetry package](https://github.com/microsoft/Agents-for-js/tree/main/packages/agents-telemetry).

:::zone-end
