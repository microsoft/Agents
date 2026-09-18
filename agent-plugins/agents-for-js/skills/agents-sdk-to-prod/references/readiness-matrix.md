# Agents SDK production-readiness matrix

Use this matrix to decide which controls apply and what evidence changes a
status. The production guide owns control details and rationale. This file owns
assessment routing; it does not replace the guide.

## Status rules

| Status | Meaning |
|---|---|
| `verified` | The control passed its required local or deployed check, and the evidence is identified. |
| `implemented` | Relevant source, configuration, infrastructure, test, or documentation exists, but required verification evidence is absent. |
| `missing` | The control applies and no sufficient implementation exists. |
| `not applicable` | The capability or risk does not exist inside the stated deployment boundary; record why. |
| `blocked` | The control applies but a named decision, dependency, permission, or environment prevents progress. |

## Required controls

Assess these for every deployed Agents SDK agent. Applicability can change the
implementation, not the need to make and record the decision.

| ID | Control | Minimum evidence |
|---|---|---|
| `BOUNDARY` | Supported channel, cloud, host, identities, callers, outbound hosts, data, dependencies, retention, scale, and owner are explicit. | Deployment documentation and rejected out-of-bound callers/channels where applicable. |
| `CONFIG` | Production configuration is typed or validated and fails startup when required values are absent. | Negative configuration tests. |
| `INBOUND_AUTHENTICATION` | Message and protected custom endpoints validate token signature, lifetime, exact audience, issuer, tenant, and selected cloud. | Unsigned, malformed, expired, wrong-audience, wrong-issuer, and wrong-tenant checks in the target environment. |
| `CALLER_AUTHORIZATION` | The deployment records whether application caller authorization applies. Trusted-app and agent-to-agent endpoints allow only expected `azp` or `appid` claims. | Authorized, unauthorized, and missing-caller checks, or a documented not-applicable channel decision. |
| `OUTBOUND_HOSTS` | Activity replies and other token-bearing SDK calls use an enabled, least-privilege outbound-host policy; arbitrary clients have equivalent destination and network controls. | Disallowed-host and service-URL-claim mismatch checks plus deployed egress configuration where applicable. |
| `SECRETS` | Credentials are excluded from source and use a secretless or managed secret path with least privilege. | Infrastructure/configuration evidence and secret scanning where available. |
| `HTTP` | Payload limits, safe middleware order, timeouts, and protected non-health routes exist. | HTTP integration tests for rejection and safe responses. |
| `TRAFFIC_PROTECTION` | Rate limits exist at an authenticated edge or another trusted caller-aware seam. | Deployed configuration and load/limit evidence; application IP limiting by connector address is insufficient. |
| `ERRORS` | Users receive generic failures; operators receive structured, redacted diagnostics. | Error-path tests and telemetry inspection. |
| `PROBES` | Liveness is dependency-independent; readiness checks required dependencies without disclosing detail. | Healthy and dependency-failure checks. |
| `LIFECYCLE` | The host drains requests and flushes telemetry during termination. | Shutdown test in the supported host. |
| `OBSERVABILITY` | Required traces, metrics, logs, attributes, sampling, redaction, retention, and alerts exist. | Telemetry inspection and alert checks. |
| `TESTS` | Build, lint where configured, unit, integration, deployment smoke, and applicable negative tests pass. | Command and deployment results. |
| `OPERATIONS` | Deployment, rollback, incident, retention/deletion, and ownership procedures exist. | Deploy guide, runbook, and exercised smoke/rollback steps. |

## Conditional controls

| Capability | Detect when | Additional controls and evidence |
|---|---|---|
| Durable state | Conversation, user, dialog, authorization, proactive, or job state survives turns or replicas. | Durable store, no production memory fallback, schema/version plan, retention/deletion, restart recovery, and bounded conflict handling where contention exists. |
| Delegated user APIs | The agent calls an API on behalf of a signed-in user. | User authorization handler, minimum scopes, sign-out, token redaction, and consent/error tests. |
| Proactive messaging | Work sends later messages or stores conversation references. | Durable protected references, authorized caller/delivery path, outbound-host validation, idempotency, and failure handling. |
| Long-running work | Work can exceed the channel or HTTP request deadline. | Durable job state, acknowledgment, authenticated status/cancellation, deadline, bounded retry, idempotency, and authorized delivery. |
| Attachments | The agent downloads, reads, transforms, or stores user files. | Shared outbound policy for supported token-bearing downloaders, source/type/size validation, scanning policy, timeout, and retention/deletion. |
| Transcripts | Activities or conversations are retained outside normal state. | Legal basis, disclosure, encryption, access review, retention, deletion, and content-safe telemetry. |
| Model calls | The agent invokes a generative model. | Bounded input/history/output, trusted instruction separation, timeout, retry/fallback, schema validation, safety policy, token/cost budgets, redacted telemetry, and evaluations. |
| Retrieval | The agent retrieves external or tenant content. | Source authorization, tenant isolation, provenance, injection resistance, bounded content, and evaluation. |
| Tools | A model or agent can invoke external operations. | Allow-list, typed server validation, least privilege, authorization, timeout, audit evidence, and no forwarded inbound credentials. |
| Side effects | Work changes external state, sends notifications, purchases, deletes, or creates resources. | Explicit approval for high-impact actions, idempotency keys, retries safe for the operation, and audit evidence. |

## Dependency rules

- Proactive messaging and long-running work require durable state unless the
  chosen provider supplies an equivalent durable mechanism.
- State retention and deletion apply whenever durable user or conversation data
  exists.
- Model-driven tools require both model-call and tool controls.
- Side-effecting tools require side-effect controls in addition to tool controls.
- Retrieval used as model input requires both retrieval and model-call controls.
- Supported attachment downloaders must reuse the adapter's outbound policy.
  Arbitrary downloads and HTTP clients require their own destination and
  network egress controls.
- A reference implementation can show a control, but only evidence from the
  user's selected environment can mark that control `verified`.

## Scope gate

A file is in scope when it directly runs, hosts, configures, secures, observes,
tests, deploys, or operates the Agents SDK agent. Do not change unrelated UI,
general business logic, shared services, or libraries merely to make them look
production-ready. Report an external dependency as `blocked` or request explicit
scope expansion when it must change for an applicable control.
