---
name: teams-app-manifest
description: Create or audit a Microsoft Teams app manifest for an existing Microsoft 365 Agents SDK application by mapping declared intent and source-code evidence to released manifest capabilities. Use when manifest.json is missing, incomplete, or must be checked against implemented Teams features. Does not configure Microsoft Entra, Azure Bot, Developer Portal, or deployment unless separately requested.
---

# Teams App Manifest

Create the smallest released Teams app manifest that accurately declares the application's implemented and intended Teams capabilities. Keep evidence for capability decisions during the work, but report only user-relevant results. Stop instead of guessing security-sensitive or product-intent values.

This is a standalone skill for Agents SDK source. Treat caller-provided requirements and repository policy as input, but derive manifest decisions only from the authority order and evidence rules in this skill.

## Execution and response contract

If the caller provides manifest-schema inspection and validation tools, use them for the
checks below instead of requiring shell execution. Preserve their exact diagnostics and
validate the final manifest before claiming success. A read-only reviewer inspects and
reports corrections without writing the manifest.

An explicit invocation of this skill with an application or source path is a request to process that application. Unless the user explicitly requests review, audit, or no changes, inspect the source and immediately perform Generate or Complete mode. Do not stop after inspection.

Generate and Complete have only two user-visible phases:

1. **Blocking questions, only when required:** Resolve values from repository evidence, caller policy, safe defaults, and approved placeholders first. If a decision still blocks an accurate manifest, ask all currently known blocking questions together. Ask direct questions only; do not return an inspection report, findings summary, capability plan, proposed mode, or implementation plan. After the user answers, resume the same workflow without repeating the findings.
2. **One final manifest report:** Create or update the manifest, perform the required local validation, then return one report using [manifest-evidence.template.md](assets/manifest-evidence.template.md). Report only added or changed manifest behavior, a short validation result, and actionable user input. Do not expose the internal capability plan or return a preliminary report before writing the manifest.

If no blocking decision remains, do not ask a question. Write the manifest and validate it in the same turn. Missing production branding, production URLs, tenant credentials, portal configuration, or deployment access does not block a local or sample manifest when repository-approved placeholders or safe sample values are available; record those limitations in the final report.

Generate or Complete is not finished until the manifest file is created or updated. Source inspection, a capability plan, and a list of missing values are internal work, not a valid final result.

Do not report unrelated repository observations such as a missing optional instruction file unless they directly block manifest correctness.

## Authority order

Use these sources in order:

1. Explicit user or repository requirements.
2. Source code, configuration, and README evidence.
3. Released Microsoft feature documentation linked from [capability-sources.md](references/capability-sources.md).
4. A released Microsoft Teams app manifest schema.
5. The existing manifest, if present.

Never derive behavior from an unreleased branch, preview-only documentation, or a similarly named API. Never upgrade a manifest only because a newer schema exists.

Coverage starts from Microsoft's [Teams capability-to-feature map](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/design/map-use-cases#app-capabilities-mapped-to-features). JSON structure and version constraints come from the released [Microsoft 365 app manifest schema](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/). The feature map is a discovery index; the schema is a structural contract. Neither replaces feature-specific documentation.

## Modes

- **Generate:** create a missing manifest from declared intent and source evidence.
- **Complete:** add required declarations to an existing manifest while preserving valid metadata and unrelated capabilities.
- **Audit:** report mismatches without changing files. This mode can audit a missing manifest by producing a proposed capability plan and unresolved-value report.

Select the mode without asking when repository state and user intent make it clear:

- If no manifest exists, use Generate.
- If a manifest exists, use Complete.
- Use Audit only when the user explicitly requests an audit, review, report, proposal, or no file changes.
- Explicit user intent overrides the repository-state default. Ask about the mode only when the request contains conflicting instructions that materially change the output.

When the user explicitly invokes this skill with an application, produce the default Generate or Complete output unless the request limits the work to review. Automatic skill selection alone does not authorize file changes. Do not present the modes as a menu. Follow the execution and response contract above.

## Required workflow

1. Select the mode using the rules above. Identify the source root, existing manifest, target environment, and whether the app is for local use, organizational distribution, or Store submission. Treat a repository sample as `local` or `sample` when no distribution target is stated; do not ask about distribution before generating it.
2. Inspect source code, routes, configuration, and README files. Do not use directory or sample names as proof of a capability.
3. Build the internal capability ledger in [capability-ledger.md](references/capability-ledger.md). The concise planning view has these columns:

   ```text
   Capability | Evidence | Manifest impact | Required values | Confidence | Source
   ```

4. Decide each candidate as:
   - `manifest-field-required`: code and intent prove a manifest declaration is required.
   - `needs-input`: implementation exists, but product intent or a value is missing.
   - `no-manifest-field`: the feature needs no additional manifest field.
   - `unsupported`: released schema or platform guidance does not support it.
5. Read only the references relevant to detected capabilities.
6. Select the repository-approved released schema version. For new Teams agent samples in this repository, use `1.22` as the baseline unless a capability or distribution rule requires a higher released version. Outside this repository, use the maintained project's baseline or request a decision. Never select an old schema only because it is technically sufficient. Record why.
7. Start from [base-manifest.template.json](assets/base-manifest.template.json) for a new manifest, or preserve the existing valid base metadata. Add only proven capability sections.
8. Run the JSON, released-schema, referenced-asset, package-structure, and source-to-manifest checks in [schema-validation.md](references/schema-validation.md). Fix in-scope errors and rerun the affected checks before the final report. Ask a blocking question only when correction requires user input or authorization.
   Before declaring completion, reconcile every ledger entry against the final manifest: every `manifest-field-required` item must identify its most specific existing manifest path, and no `needs-input` item may remain unresolved.
9. After implementation and validation, return the final manifest report required by the execution contract. In Generate mode, report only additions. In Complete mode, report additions and changes; mention preserved behavior only when preservation is important to the user or prevents accidental breakage. Never list omitted capabilities in Generate or Complete reports.
10. Do not create `manifest-evidence.md` by default. Persist the report only when the user explicitly requests a file or caller-provided policy requires one. Return no other findings report before the final report.

## Decision boundaries

Do not infer these values without explicit requirements or conclusive repository evidence:

- Installation scopes: `personal`, `team`, `groupChat`, or `copilot`.
- Microsoft Entra application IDs, application ID URI, tenant model, or OAuth configuration.
- RSC, Graph, delegated, or application permissions.
- Public domains, privacy URL, terms URL, or production endpoints.
- Store distribution requirements.
- `isNotificationOnly`, Copilot exposure, calling, video, or cross-host support.

Route attributes prove that code can handle an activity. They do not always prove that the product intends to expose that capability or scope.

If a required decision is missing, do not emit a guessed value. Use a clear placeholder only when the repository already has a documented environment-substitution convention; otherwise stop and report `needs-input`.

Resolve required values in this order: existing repository value, caller-provided policy, safe documented default, conclusive source evidence, then user input. Do not ask the user for a value that can be resolved safely. Never use a default for a security-sensitive or product-intent decision.

For new manifests, omit optional properties when the intended value equals the released schema default. Emit a default value only when feature documentation or repository convention requires it. In Complete mode, preserve existing explicit defaults to avoid unrelated churn. Read [schema-validation.md](references/schema-validation.md) for the full emission policy.

## Feature routing

- For Agents SDK source signals, read [source-detection.md](references/source-detection.md), then load the feature reference it selects.
- Use [capability-ledger.md](references/capability-ledger.md) to record and reconcile every detected candidate before completing the manifest.
- For bot scopes, commands, proactive messages, files, targeted messages, calling, cards, or Copilot exposure, read [bots.md](references/bots.md).
- For Adaptive Cards and dialogs/task modules, read [cards-and-dialogs.md](references/cards-and-dialogs.md).
- For query/action commands, item selection, configuration, or link unfurling, read [message-extensions.md](references/message-extensions.md).
- For meeting events, participant events, notifications, transcripts, roles, or calling, read [meetings.md](references/meetings.md).
- For SSO, `webApplicationInfo`, RSC, Graph access, and domain decisions, read [authentication-and-permissions.md](references/authentication-and-permissions.md).
- For tabs, webhooks, connectors, or Graph conversational features, start with [capability-sources.md](references/capability-sources.md) and follow its official feature documentation. Do not force these features into a bot manifest.
- For schema selection, packaging, and validation, read [schema-validation.md](references/schema-validation.md).

## Fail-closed rules

- No evidence: do not add the capability.
- Ambiguous scope or product intent: report `needs-input`.
- Missing domain, ID, URL, permission, command metadata, or icon: first resolve it through repository convention, an approved placeholder or reusable asset, or a safe local/sample value. Ask only when no safe resolution exists and the missing value blocks an accurate manifest.
- Documentation and schema disagree: stop and cite both.
- Capability requires Developer Portal, Entra, Azure, Graph, or subscription configuration: generate only the verified manifest part and list the external setup separately.
- Existing manifest contains a capability not found in code: preserve it in Complete mode and flag it for review; never remove it automatically. If that capability is invalid under its declared schema or violates a required coupling, stop and report it instead of preserving it into a claimed-valid result.
- Validation passes but source-to-manifest evidence fails: the manifest is not complete.

## Invocation contract

A caller can supply the source root, existing manifest if any, intended Teams scopes, distribution target, repository placeholder convention, and other explicit product requirements. Treat these as inputs, not as permission to bypass schema, evidence, security, or validation rules.

The caller owns interaction and automation policy. This skill returns unresolved decisions as `needs-input`; it does not decide whether the caller asks a user, records the decision for later, or stops a larger workflow.

Return:

- `manifest.json` or an audit-only diff.
- A user-facing manifest report containing only changes, a short validation summary, and actionable `needs-input` items, including external configuration that cannot be represented in the manifest.
- A persisted evidence file only when explicitly requested or required by caller-provided policy.
