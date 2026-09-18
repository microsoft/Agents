---
name: sync-teams-dotnet-samples-to-agents-sdk
description: CI instructions for a bounded Teams SDK .NET to Agents SDK sample synchronization.
---

# Teams sample synchronization

This CI caller uses one persistent implementation session. Preserve the source behavior with
the smallest necessary changes inside the selected sample. Invoke the public migration skill
before editing code and the manifest skill when updating the Teams app package.

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
