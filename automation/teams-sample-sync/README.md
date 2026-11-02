# Teams sample synchronization

This workflow synchronizes selected Teams SDK .NET and JavaScript/TypeScript samples into Agents SDK
counterparts, including initial imports. Its jobs are plan, migrate, and handoff. Migration has Copilot access but no
repository-write credential. The handoff job verifies the candidates, uploads their patches and
migration evidence, and creates an issue for Copilot cloud agent. Copilot owns the resulting
implementation and PR, including its title, description, and readiness.

Synchronization owns all maintenance of generated Teams samples in every language profile
defined in `config/targets.yml`, currently under `samples/dotnet/teams` and
`samples/nodejs/teams`. This includes SDK versions, lockfiles, shared-helper copies,
and compatibility repairs, and applies to future language profiles. The repository's
[Copilot instructions](../../.github/copilot-instructions.md) apply to ordinary tasks and review;
workflow-generated handoff issues with prepared patches and migration evidence authorize
Copilot to complete this maintenance. Arbitrary issue assignment does not. Automation source
and the shared contract harness outside those roots can still be maintained through ordinary
tasks. Samples outside the configured generated destination roots are not covered by this rule.

The frozen migration plan includes the selected SDK update and required API migrations together.
The coordinator selects the newest supported stable .NET line shared by required packages within
the configured major, and freezes compatible exact JS package versions. Neither policy means
arbitrary latest majors, prereleases, or unrelated dependency upgrades. The full selection is
fingerprinted: a changed .NET release tag schedules revalidation even within the same selected
line, while identical verified inputs and output remain unchanged. This does not guarantee
detection of every independent NuGet package publication.

The .NET helper's authoritative source is `microsoft/Agents-for-net` at
`src/samples/Shared/AspNetExtensions.cs`. The configured canonical sample,
`samples/dotnet/quickstart`, supplies the local synchronized copy and is fingerprinted as a
migration input. Refreshing that input requires separate maintenance before synchronization;
the migration session may only update helper copies inside its selected sample. It must report
missing or incompatible canonical input rather than fetching an unpinned replacement.

The workflow runs every Sunday at 00:00 UTC for all configured samples. Dispatch manual runs from
the repository's default branch; other refs are skipped. Manual runs can select one
language (`all`, `dotnet`, `js`), a sample or all samples, and publication. A named sample with
`language=all` selects its configured language variants; an absent combination fails.
Migrations run independently, but the
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
SDK API compiler errors must be repaired while preserving source, manifest, and contract behavior.
Do not downgrade below the selected target, restore obsolete packages, remove capabilities,
weaken tests, or suppress diagnostics to pass. Full validation follows implementation and repair.
Persistent failures, shared-harness incompatibility, and infrastructure blockers prevent successful
publication and verified-state advancement; they do not expand the agent's write permissions.

The handoff job verifies every patch against the planned base and uploads one bundle containing
`plan.json` and each sample's patch, migration plan, self-audit, source context, and result metadata.
Per-sample diagnostic artifacts retain the full
migration logs. The bundle does not contain generated PR titles, descriptions, or agent instructions.

After uploading the bundle, the workflow creates or reuses an issue with the target branch,
workflow and artifact links, sample summaries, and short acceptance criteria. Copilot uses the
prepared migration as a starting point, makes any adjustments needed, validates its work, and
creates a linked PR. The issue asks for a summary of each sample's changes (including no-change
outcomes), additional adjustments and why, validation results, and remaining manual checks.
The handoff explicitly carries synchronization ownership. After any adjustments, Copilot must
rerun relevant full validation and keep unresolved failures visible and the PR not ready to merge.
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

The .NET contract host installs offline connector and user-token transports before
each turn, as required by MSTeams 1.9. Unexpected Teams API requests fail explicitly.
`AgentsSdkTestVersion` selects both the testing package and the Teams extension.
To check host compatibility with 1.9 independently of sample migration:

```sh
dotnet test automation/teams-sample-sync/tests/contracts/TeamsSampleSync.ContractTests.csproj \
  '-p:AgentsSdkTestVersion=1.9.*' --filter FullyQualifiedName~TeamsExtension_Initializes
```

The .NET Teams samples and contract suite now default to SDK 1.9. Run the complete
suite above to check route signatures and payload behavior across all seven samples.
When full validation fails, the terminal error reports the sample, failed checks,
and their errors. The generic "Current full validation is required" message is
reserved for partial, mismatched, or missing validation identity evidence.

Profiles live in `config/targets.yml`. Both languages configure the same seven sample names.
Their state paths are separate: `state/dotnet/<sample>.lock.json` and
`state/js/<sample>.lock.json`. Old unqualified .NET locks supply upstream history
only; the first run after this upgrade revalidates them. A JS target never reads a
legacy .NET lock. Shared automation changes conservatively revalidate both languages.

JavaScript/TypeScript samples live in `samples/nodejs/teams/<sample>`. The resolver
freezes compatible stable exact versions of four Agents SDK npm packages in the
minimum version's major; it checks dependency/peer ranges and Node 24 support.
Each profile owns its guidance, protected contracts, manifest asset source and
generated-output exclusions. Python will need its own adapter/profile and runtime
setup; it is not enabled here.

The JS validator prepares the npm lockfile, installs with lifecycle scripts disabled,
checks the installed SDK versions, compiles fresh output, runs protected contracts,
checks the released manifest schema and starts the HTTP endpoint. Its child
processes receive a sanitized environment. Run sample checks locally with:

```sh
cd samples/nodejs/teams/bot-cards
npm ci --ignore-scripts
npm run build
cd ../../../..
node automation/teams-sample-sync/tests/js-contracts/bot-cards.test.mjs samples/nodejs/teams/bot-cards
```

Repeat for each configured sample. These contracts cover local SDK routing and
mock external services; credentialed Teams/Graph behavior still needs manual checks
described in each sample's README. The offline candidate integration tests exercise
initial and incremental patch/state verification with fake Copilot/npm transports.

The sync workflow keeps its existing filename to preserve links, with a generic
display name and language-qualified matrix/artifact IDs. It still dispatches only
from the default branch. Normal sample PR CI explicitly discovers immediate Node
packages and `samples/nodejs/teams/*`, builds on Node 22/latest, and runs the Teams
contracts. For registered GitHub-native stacks, Actions evaluates PR branch filters against
the stack trunk. Verify the hosted checks on each layer. For a branch-only chain,
run the listed checks locally and after retargeting to `main`.

The live Copilot smoke test is opt-in because it consumes usage. Set
`TEAMS_SYNC_SDK_SMOKE=1` before running `npm run test:sdk-smoke --prefix automation/teams-sample-sync`.

## Maintaining the automation

Keep hand-written files below 500 physical lines. The scoped rules in
`eslint.config.js` require braced control flow, readable object properties and
spacing before returns. Run `npx eslint automation/teams-sample-sync` from the
repository root alongside the build and tests above.

| Responsibility | Start here |
| --- | --- |
| CLI commands and artifacts | `src/cli.ts`, `src/cli-io.ts` |
| Configuration and safe target paths | `src/config.ts`, `src/identity.ts` |
| Incremental decisions and frozen input fingerprints | `src/plan.ts`, `src/plan-inputs.ts`, `src/state.ts` |
| Copilot permissions and migration protocol | `src/agent-runner.ts`, `src/sync-session.ts`, `src/guard.ts` |
| Validation orchestration and language adapters | `src/validate.ts`, `src/languages.ts`, `src/*-validation.ts` |
| Source capabilities and manifest requirements | `src/js-capabilities.ts`, `src/dotnet-capabilities.ts`, `src/manifest-capabilities.ts`, `src/manifest.ts` |
| Patch verification and coordinated handoff | `src/verify-patch.ts`, `src/publish-bundle.ts`, `src/handoff-bundle.ts` |

Keep real SDK behavior checks in `tests/js-contracts` and `tests/contracts`.
Use `tests/unit` for deterministic policy checks and `tests/integration` for the
offline candidate pipeline. Shared session doubles live in `tests/fixtures`;
outcome reconciliation and manifest validation have their own test modules.

`scripts/build.mjs` removes stale compiled output before invoking TypeScript.
Build scripts participate in the validator input fingerprint, just like source.
Readability edits therefore trigger conservative revalidation of verified samples.
