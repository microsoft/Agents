# Teams sample synchronization

This workflow synchronizes selected Teams SDK .NET samples into their existing Agents SDK
counterparts. Its jobs are plan, migrate, and handoff. Migration has Copilot access but no
repository-write credential. The handoff job verifies the candidates, uploads their patches and
migration evidence, and creates an issue for Copilot cloud agent. Copilot owns the resulting
implementation and PR, including its title, description, and readiness.

The workflow runs every Sunday at 00:00 UTC for all configured samples. Dispatch manual runs from
the repository's default branch; other refs are skipped. Manual runs can select one
sample or all samples and can enable or disable publication. Migrations run independently, but the
selected samples publish as one coordinated set: if any migration fails, no handoff issue is created.
Planning fetches `upstream.ref` from `config/targets.yml` into a bare Git repository, so no upstream
code is checked out in the planning job. Migration jobs use the exact commit selected by the plan.
A sample's deadline aborts Copilot and cancels validation processes.

For each changed sample, one persistent Copilot implementation session first creates a Markdown
migration plan without write permission. The coordinator saves and hashes that plan. The same
session then uses the Teams-to-Agents and manifest skills to implement it, validate its work, and
self-audit the final files against the frozen plan. The workflow independently runs the full
validator, allows one repair turn for deterministic failures, and publishes only a verified patch.
If the implementer's reported outcome disagrees with the selected sample's actual changed files,
the session gets one reconciliation turn and another full validation pass. A continuing mismatch fails
the sample.
When verification finds that no selected-sample file needs a change, the published patch contains
only synchronization state and is explicitly reported as **no changes required**.

The handoff job verifies every patch against the planned base and uploads one bundle containing
`plan.json` and each sample's patch, migration plan, self-audit, source context, and result metadata.
Per-sample diagnostic artifacts retain the full
migration logs. The bundle does not contain generated PR titles, descriptions, or agent instructions.

After uploading the bundle, the workflow creates or reuses an issue with the target branch,
workflow and artifact links, sample summaries, and short acceptance criteria. Copilot uses the
prepared migration as a starting point, makes any adjustments needed, validates its work, and
creates a linked PR. The issue asks for a summary of each sample's changes (including no-change
outcomes), additional adjustments and why, validation results, and remaining manual checks.
There is no post workflow, PR metadata rewrite, exact-patch PR gate, or automatic readiness step.

Failed runs retain their diagnostic artifacts and can be rerun. Reruns reuse open issues when the
prepared base and verified outputs match. An already assigned issue is left untouched; an unassigned
issue is updated with the current artifact before assignment. If the target branch moves before
assignment, rerun synchronization to prepare changes against the current base.

The optional Actions secret `COPILOT_ASSIGNMENT_TOKEN` enables automatic assignment. When configured,
the workflow uses this GitHub user token to create or update the issue and assign Copilot. The token
must have permission to assign Copilot in this repository. If assignment fails, the handoff fails
and reports the error.

Without the secret, the workflow uses `GITHUB_TOKEN` to create or update the issue, then succeeds
with an issue link and instructions to assign Copilot manually in the workflow summary. The handoff
job grants `GITHUB_TOKEN` issue-write permission and read-only repository contents and Actions access.
Issues already assigned to Copilot remain untouched in either mode. The workflow does not push a
branch or create a PR itself.

Copilot uses Auto routing. The workflow records the observed model for diagnosis but does not
select a model, enumerate a model catalog, or force a reasoning effort.

The workflow preserves pinned source commits, source context, selected-sample write confinement,
full validation, incremental state, and patch verification. It does not use a reviewer session,
agent-authored evidence schemas, or policy configuration.

Run locally from the repository root:

```sh
npm ci --prefix automation/teams-sample-sync
npm test --prefix automation/teams-sample-sync
npm run build --prefix automation/teams-sample-sync
dotnet test automation/teams-sample-sync/tests/contracts/TeamsSampleSync.ContractTests.csproj
```

The live Copilot smoke test is opt-in because it consumes usage. Set
`TEAMS_SYNC_SDK_SMOKE=1` before running `npm run test:sdk-smoke --prefix automation/teams-sample-sync`.
