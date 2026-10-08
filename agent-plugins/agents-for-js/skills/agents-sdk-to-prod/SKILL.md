---
name: agents-sdk-to-prod
description: >
  Use when a user wants to assess, create, harden, deploy, or review the
  production readiness of JavaScript or TypeScript code built with the
  Microsoft 365 Agents SDK. Do not use for unrelated applications, services,
  or code that does not run or support an Agents SDK agent.
---

# Move an Agents SDK sample toward production

Guide the user from an Agents SDK starter or existing agent to a bounded,
evidence-backed production deployment. The outcome is not only changed source:
it includes an explicit deployment target, applicable controls, verification
evidence, and remaining production work.

## Scope

Change only code, configuration, infrastructure, tests, and operations
documentation that directly run, host, secure, observe, deploy, or support the
Agents SDK agent. Preserve unrelated application code, user interfaces,
services, libraries, and business behavior. If a required control needs a
change outside this scope, explain the dependency and get user approval before
expanding the task.

This skill applies to code built with `@microsoft/agents-hosting`,
`@microsoft/agents-hosting-express`, or related Microsoft 365 Agents SDK
packages. It is not a general production-readiness skill.

## Sources

1. Read the production guide first and treat it as normative. In a checkout of
   `microsoft/Agents`, prefer
   `docs/production-ready-agent-js.md`; otherwise use
   [the published guide](https://github.com/microsoft/Agents/blob/main/docs/production-ready-agent-js.md).
2. Read [the readiness matrix](references/readiness-matrix.md) for assessment,
   dependency, status, and evidence rules.
3. Use `samples/nodejs/production-reference/` when it exists locally;
   otherwise use
   [the published production reference](https://github.com/microsoft/Agents/tree/main/samples/nodejs/production-reference).
   It proves one Web Chat, App Service, and Blob profile. It is not a universal
   template.
4. Inspect the installed SDK version and APIs before implementing changes. The
   guide defines the control; the installed SDK determines the valid code.

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
  and audience rejection, applicable caller authorization, service URL claim
  and outbound-host validation, payload rejection, safe errors, probes,
  graceful shutdown, and telemetry shutdown/redaction.
- Validate state recovery only when state exists. Validate conditional controls
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

## Rules

- Do not use `MemoryStorage` in a production deployment.
- A stateless agent does not need durable state. Any agent state that must
  survive a turn, restart, or replica must use a durable store.
- Protect the message endpoint and every non-health endpoint that changes data,
  sends messages, or calls a downstream API.
- Do not add `authorizeJWT` when `startServer` or
  `createAgentRequestHandler` already owns JWT validation. Use an explicit
  adapter pipeline when caller or channel authorization must run after JWT
  validation and before activity processing.
- Enable issuer validation in production. Configure the expected tenant and
  cloud-specific authority or issuers.
- Decide whether application caller authorization applies. For trusted-app or
  agent-to-agent endpoints, authorize the verified `azp` or `appid` claim
  before adapter processing.
- Treat the SDK outbound-host policy as opt-in and enable it explicitly in
  production. Decide whether the selected channel needs the built-in Microsoft
  hosts or a complete explicit host set. Configured hosts are suffix rules and
  also allow subdomains. Apply separate destination and network egress controls
  to arbitrary HTTP, model, retrieval, and tool clients.
- For local tools that use loopback service URLs, disable the validator or allow
  only the required local host. Never carry a local-host exception into
  production.
- When the installed SDK requires `connectionsMap.serviceUrl=*` to select a
  default named connection, treat the wildcard only as connection selection
  and keep exact audience and outbound-host validation enabled. Add channel-host
  middleware after JWT validation only when the deployment boundary is narrower
  than the SDK host policy.
- Reuse the outbound policy with supported attachment downloaders. Do not
  assume that adapter validation protects arbitrary downloads or HTTP calls.
- Do not describe an agent as production-ready without deployment, test, and
  operator evidence. Use the maturity states instead.
- Keep the user’s scenario small. A production reference demonstrates architecture, not every SDK feature.
