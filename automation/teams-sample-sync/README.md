# Teams sample synchronization

This workflow synchronizes selected Teams SDK .NET samples into their existing Agents SDK
counterparts. It has three GitHub Actions jobs: plan, migrate, and publish. Migration has Copilot
access but no repository-write credential. Publishing has repository-write access but never starts
Copilot or executes the candidate.

The workflow runs every Sunday at 00:00 UTC for all configured samples and automatically creates
draft pull requests for publishable results. Manual runs can select one sample or all samples and
can enable or disable draft pull request creation.

For each changed sample, one persistent Copilot implementation session first creates a Markdown
migration plan without write permission. The coordinator saves and hashes that plan. The same
session then uses the Teams-to-Agents and manifest skills to implement it, validate its work, and
self-audit the final files against the frozen plan. The workflow independently runs the full
validator, allows one repair turn for deterministic failures, and publishes only a verified patch.
When verification finds that no selected-sample file needs a change, the published patch contains
only synchronization state and is explicitly reported as **no changes required**.

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
