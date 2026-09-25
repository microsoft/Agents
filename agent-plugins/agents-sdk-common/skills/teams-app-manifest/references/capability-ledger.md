# Capability ledger

Use this internal ledger to prevent source inspection, manifest editing, and final validation from becoming disconnected. It is working evidence, not a user-facing artifact, and is not persisted unless the caller explicitly requires it.

## Discovery

Inspect source code, routes, configuration, tests, and README documentation. Record every plausible Teams-facing capability before deciding whether it affects the manifest. Do not use a sample name as evidence. Read the relevant feature reference for each candidate.

Each entry contains:

```text
id | kind | evidence | decision | manifestPath | reference
```

- `id`: stable lowercase identifier unique within this manifest assessment.
- `kind`: concise feature category, not a schema field guess.
- `evidence`: one or more concrete source paths, symbols, routes, tests, or README sections.
- `decision`: `manifest-field-required`, `no-manifest-field`, `needs-input`, or `unsupported`.
- `manifestPath`: the most specific actual JSON path in the final manifest for `manifest-field-required`; `none` otherwise.
- `reference`: the manifest-skill reference that supports the decision.

Keep independently decidable surfaces in separate entries. In particular, a conversational
bot/message-routing entry must not absorb user-facing command discovery: runtime dispatch and
manifest exposure have different evidence and can reach different decisions.
Each `manifest-field-required` entry maps to exactly one manifest field path. When a behavior
requires multiple manifest declarations, create one stable capability entry per declaration;
never combine paths with commas, prose, selectors, or array expressions.

When an automation requests machine-checkable paths, use a concrete dotted path with numeric
array indexes, such as `authorization.permissions.resourceSpecific[0].name`. Inspect the final
manifest to obtain the index. Do not use wildcards, empty indexes, or semantic selectors such
as `[name: ...]`; those are descriptions, not paths to an actual JSON value.

## Reconciliation rules

- `manifest-field-required` means evidence and intended behavior require a declaration. Write it and record the most specific actual final JSON path representing it. Do not use `bots[0]` when the capability requires a child field such as a command list or feature flag.
- `no-manifest-field` means the capability was considered but the applicable reference says it exists only in code, an activity payload, or external setup. Use path `none`.
- `needs-input` means product intent or a value cannot be resolved safely. Use path `none`. Generate other verified manifest content first when possible.
- `unsupported` means the required behavior is unavailable in the selected released platform/schema. Use path `none`.
- Do not finish Generate or Complete with a missing required path or an unresolved `needs-input` entry.
- Schema validity is necessary but does not prove this ledger matches the application's behavior.
- External portal, identity, domain, deployment, tenant, or subscription work does not invalidate a manifest fragment supported by evidence and repository-approved placeholders. Record that work separately.

Automation callers may request the ledger as structured in-memory report data so an independent reviewer can reconstruct and compare it. Do not create `manifest-evidence.md` or another ledger file by default.
