---
name: agents-sdk-to-prod
description: >
  Assess, create, harden, deploy, or review production readiness of Python
  Microsoft 365 Agents SDK agents and their direct hosting/operations files.
  Use only for Agents SDK scenarios, not unrelated application hardening.
---

# Move a Python Agents SDK agent toward production

Use this skill for `microsoft_agents.hosting.core`, `microsoft_agents.hosting.aiohttp`, and related SDK packages. Preserve the selected scenario and change only
files that directly run, host, secure, observe, test, deploy or operate the agent.
An external dependency requiring changes outside that scope needs an explicit
scope decision. A question or review request is read-only.

## Sources

1. Read `docs/python/production-ready-agent.md` in the Agents checkout, or
   [the language guide](https://github.com/microsoft/Agents/blob/main/docs/python/production-ready-agent.md).
   Follow its links to the shared controls. The guide is normative.
2. Read [the packaged readiness matrix](references/readiness-matrix.md) for
   control IDs, statuses, applicability, dependencies and evidence rules.
3. Inspect `samples/python/production-reference/`, or
   [the reference](https://github.com/microsoft/Agents/tree/main/samples/python/production-reference).
   It demonstrates one bounded Web Chat/App Service/Blob profile.
4. Inspect the target's actual SDK APIs and dependency baseline. The reference
   uses stable released packages; verify the installed APIs before changing the
   user's dependency version or selecting production controls.

## Workflow

### 1. Discover

- Determine whether the user wants a new sample, an assessment, or changes to
  an existing sample. Do not ask when the request or repository already shows
  the answer.
- Inspect the candidate Agents SDK sample before asking questions. If multiple
  samples exist, identify the requested sample or ask which one is in scope.
- Identify channel, cloud, host, inbound audiences and issuers, trusted calling
  applications, downstream identities, outbound hosts, state, external
  dependencies, data classes, retention, scale, side effects, long-running
  work, model calls, retrieval, tools, token-bearing downloaders, attachments,
  transcripts, and proactive messaging.
- State the deployment boundary. Do not broaden channel, host, identity, data,
  or application scope without user approval.

### 2. Assess

- Classify every applicable readiness control as `verified`, `implemented`,
  `missing`, `not applicable`, or `blocked` using the readiness matrix.
- Cite repository evidence for each `verified` or `implemented` result. Do not
  infer production evidence from the presence of source code.
- For review or question-only requests, stop at guidance and assessment. Do not
  modify files.

### 3. Agree

- Recommend the smallest suitable production profile and defaults.
- Ask only for unresolved decisions that materially affect architecture, cost,
  identity, channel, data handling, or side effects.
- Present the implementation sequence and identify changes that would extend
  beyond Agents SDK-related code. Obtain approval for that expansion.

### 4. Implement

- Add required controls first. Add conditional controls only for capabilities
  present in the selected scenario.
- Preserve the agent's intended behavior. Do not refactor unrelated code as
  part of production hardening.
- Add or update tests and operations documentation with each control. Keep
  secrets and sensitive user content out of source, generated files, logs,
  errors, prompts, and telemetry.

### 5. Verify

- Run the repository-supported build, lint, unit, and integration checks that
  apply to the changed Agents SDK code.
- Exercise failed production configuration, authentication rejection, issuer
  and audience/lifetime rejection, applicable caller authorization, service URL claim
  and outbound-host validation, payload rejection, safe errors, probes,
  graceful shutdown, and telemetry shutdown/redaction.
- Validate state recovery and conflict handling only when state exists. Validate conditional controls
  only for capabilities in scope.
- Separate local verification from deployed evidence. Do not claim that a
  control is verified when its required environment was not exercised.

### 6. Handoff

Report:

1. The selected deployment boundary and current maturity state.
2. Controls verified with file, test, deployment, or operator evidence.
3. Controls implemented but not verified in the required environment.
4. Missing or blocked controls and their production impact.
5. Controls marked not applicable and why.
6. The next deployment, smoke-test, rollback, and operations actions.

Keep this assessment current through follow-up discussion. Answer questions
without losing the selected boundary, decisions, statuses, or next phase.

## Maturity states

- **Code-hardened:** applicable source controls exist and local checks pass.
- **Deployment-ready:** infrastructure, production configuration, smoke tests,
  rollback, and operator documentation exist.
- **Production-verified:** the deployed environment has passed authentication,
  state recovery where applicable, telemetry, alert, traffic-protection, smoke, and
  rollback checks.

## Language invariants

- For aiohttp, use SDK JWT middleware/decorators exactly once on protected routes.
  Keep health endpoints outside authentication and explicitly protect new custom routes.
- Set `AgentAuthConfiguration.validate_issuer=True`, require exact audience and
  expiration, disable anonymous production access, and select the expected token
  family, tenant and cloud. Authorize verified `claims_identity` caller claims for
  trusted-app endpoints before adapter processing.
- Verify the issuers actually accepted by the installed SDK. Its trusted
  first-party Entra issuers can extend the configured issuer list. For the bounded
  public-cloud Bot Framework profile, additionally require the verified
  `claims_identity` issuer to equal `https://api.botframework.com` after JWT
  validation and before adapter processing. Other profiles need their own issuer
  policy; do not apply this Bot-only restriction to trusted-app endpoints.
- Pass `OutboundHostValidator` explicitly to `CloudAdapter`. Confirm downloader
  support before sharing it; host suffix validation alone does not provide DNS or
  arbitrary-client protection.
- Use the Blob service URL (not container URL) with `BlobStorageConfig` and async
  credentials. The released SDK overwrites state unconditionally; use the reference's
  ETag wrapper or an equivalent durable concurrency-safe provider when needed.
- Preserve async cleanup: drain aiohttp, close clients/credentials, then flush
  telemetry. Verify published package versions and deployment wheel hashes.

## Shared invariants

- Production state that must survive turns/restarts/replicas requires a durable
  store. Stateless agents do not need a state provider.
- Wildcard connection selection is not authorization. Keep exact audience,
  verified issuer/caller checks, and outbound policy.
- Validator host entries can allow subdomains. Exact channel middleware and
  arbitrary-client/network controls remain separate decisions.
- Health endpoints may be anonymous; routes with mutations/downstream access
  require authentication and applicable authorization.
- Caller-aware edge protection, deployed alerts, recovery and rollback require
  environment evidence. Do not certify a deployment from sample code.
- Keep secrets, tokens, prompts/messages and unnecessary identity attributes out
  of errors, logs, generated artifacts and telemetry.
