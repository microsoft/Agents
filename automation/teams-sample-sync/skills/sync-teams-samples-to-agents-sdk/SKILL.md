---
name: sync-teams-samples-to-agents-sdk
description: CI instructions for a bounded Teams SDK to Agents SDK sample synchronization using the selected language profile.
---

# Teams sample synchronization

This CI caller uses one persistent implementation session. Preserve the source behavior with
the smallest necessary changes inside the selected sample. Invoke the public migration skill
before editing code and the manifest skill when updating the Teams app package.
The context identifies the language and frozen SDK selection. For .NET, use its
resolved release line; for JS/TS, use its exact npm package map. The minimum is
a floor. Apply the selected migration skill's package conventions rather than
copying the minimum into dependencies or imposing one language's version syntax
on another. Initial imports may have an empty destination.

Plan and implement SDK updates together with the compatibility migrations they require.
"Keep current" means the newest supported selection under the coordinator's stable-release,
supported-major and common-package rules, including JS package compatibility; it does not
authorize arbitrary latest majors, prereleases, or unrelated third-party dependency upgrades.
Treat compiler errors from selected SDK APIs as implementation work inside the selected sample.
Preserve source, manifest, and contract behavior, including meeting timestamps/transcripts,
task dialogs, message-extension previews, signed upload URLs, and targeted recipients.
Do not restore obsolete API packages, downgrade below the selected target, remove features or
handlers, weaken tests, or suppress diagnostics to obtain a passing result.

For .NET shared helpers, the authoritative `AspNetExtensions.cs` comes from
`microsoft/Agents-for-net` at `src/samples/Shared/AspNetExtensions.cs`. The configured canonical
sample (`automation/teams-sample-sync/reference/dotnet`) supplies a local synchronized copy, fingerprinted by the
coordinator. Use that supplied input to maintain copies inside the selected sample as needed.
Do not download an unpinned replacement or edit the canonical sample from this session.
If the input is missing or needs refreshing for the selected SDK, report that prerequisite.

Preserve Agents terminology in README/catalog descriptions and user-facing
messages: use "agent" for the migrated runtime even when upstream says "bot".
Keep platform names (Azure Bot), schema keys (`bots[]`, `botId`), and existing
source-derived identifiers. Treat this as an intentional migration difference,
not a behavior mismatch to repair. Do not weaken tests to restore upstream wording.

The first turn is read-only planning. Before finalizing the plan, reconcile every user-facing
capability across source handlers, README/help text, the destination implementation, and the
actual destination manifest JSON. For commands, record the exact title, slash or mention surface,
handler evidence, and whether it exists under `bots[].commandLists`. Treat documentation as a
requirement to verify, not proof of the manifest's current contents. A claim that no manifest
change is needed must cite the relevant JSON path and current value.

Return a concise Markdown specification with an item for each relevant source behavior: upstream
path and stable symbol, destination change, Agents-specific behavior to preserve, manifest impact,
and validation expectation. Include every mismatch found during capability reconciliation. The
coordinator freezes that plan before it grants write access.

After write access is granted, implement the frozen plan without editing it. The only CI tool is
`validate_sample`; use `code` during implementation, `manifest` when editing the package, and
`all` before completing the task. A validation result is authoritative only for the current
candidate. Add meaningful selected-sample tests when the behavior can be tested locally.

Before full validation, re-open the final implementation, README/help text, and manifest. Compare
them against the capability inventory by exact command title and surface; schema validation alone
does not prove capability coverage. Finish by returning a Markdown self-audit that accounts for
every frozen-plan item and names the validation run. Start it with `## Self-audit`, then write
`Outcome: changed` or `Outcome: no changes required` on its own line. A no-changes outcome requires
evidence that every plan item was verified and that no selected-sample file needs modification. If
a required behavior cannot be implemented, state the actual blocker. Do not claim validation passed
when it did not. The coordinator may request one repair for deterministic validation failures; keep
the frozen plan unchanged during that repair.
Run full validation again after repairs. Shared-harness incompatibility, missing canonical input,
unavailable package feeds, and infrastructure failures must remain visible as blockers; they do
not authorize protected-file edits or repeated repair attempts. An unsuccessful migration must
not be reported as a successful sync or handed off as a validated candidate.
