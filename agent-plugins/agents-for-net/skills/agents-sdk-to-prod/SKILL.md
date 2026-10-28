---
name: agents-sdk-to-prod
description: >
  Assess, create, harden, deploy, or review production readiness of C#/.NET
  Microsoft 365 Agents SDK agents and their direct hosting/operations files.
  Use only for Agents SDK scenarios, not unrelated application hardening.
---

# Move a C#/.NET Agents SDK agent toward production

Use this skill for `Microsoft.Agents.Hosting.AspNetCore`, `Microsoft.Agents.Builder`, and related SDK packages. Preserve the selected scenario and change only
files that directly run, host, secure, observe, test, deploy or operate the agent.
An external dependency requiring changes outside that scope needs an explicit
scope decision. A question or review request is read-only.

## Sources

1. Read `docs/dotnet/production-ready-agent.md` in the Agents checkout, or
   [the language guide](https://github.com/microsoft/Agents/blob/main/docs/dotnet/production-ready-agent.md).
   Follow its links to the shared controls. The guide is normative.
2. Read [the packaged readiness matrix](references/readiness-matrix.md) for
   control IDs, statuses, applicability, dependencies and evidence rules.
3. Inspect `samples/dotnet/production-reference/`, or
   [the reference](https://github.com/microsoft/Agents/tree/main/samples/dotnet/production-reference).
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

- SDK service registration does not authenticate routes. Configure ASP.NET JWT
  validation and require authorization explicitly on message/protected endpoints.
  Helpers can vary by installed SDK version; do not layer duplicate pipelines.
- Require signed, unexpired tokens with exact audience and configured issuer,
  tenant/cloud policy. Preserve original claim names when authorizing verified
  `azp`/`appid` for trusted-app endpoints. Distinguish Bot Service token handling.
- Configure outbound authentication scopes for the selected channel. The public
  Bot Framework profile uses `https://api.botframework.com/.default` in
  `Connections:ServiceConnection:Settings:Scopes`; inbound token validation does
  not configure outbound scopes. Verify the installed authentication provider's
  requirements and use the appropriate scopes for other clouds or downstream APIs.
- Inspect the adapter's error path as well as application error handlers. Install
  a safe `CloudAdapter.OnTurnError` handler when the default can expose exception
  details. Return a generic message and record only redacted telemetry; verify an
  actual failing turn through the adapter, not only a direct handler call.
- Enable and register `IOutboundHostValidator` explicitly. Validate exact channel
  hosts when narrower than suffix rules. Inspect async address-check coverage and
  apply separate destination/egress controls to arbitrary HttpClient calls.
- Register durable `IStorage`; preserve Blob ETags and handle conflicts without
  replaying complete turns or duplicating side effects. The released AgentState
  writes plain dictionaries; use the reference's conditional Blob provider or an
  equivalent provider that carries versions per turn. Use explicit managed
  identity and no production memory fallback.
- Let ASP.NET hosting drain requests before telemetry flush/provider disposal.
  Verify released SDK package versions and resolved build dependencies.

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
