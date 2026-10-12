# Documentation handoff

## Proposed repository changes

Copy `production-ready-agent.md` to `microsoft-365-agents/production-ready-agent.md` in `MicrosoftDocs/businessapps-copilot-docs-pr`. This is a new, consolidated how-to article with persistent Python, Node.js, and .NET pivots. The existing language guides and shared contract remain source documentation in the samples repository.

This bundle was authored in handoff mode: the Learn repository, navigation files, build configuration, and rendered Learn preview were unavailable. No includes or pivot-group changes are proposed. The article is self-contained and doesn't depend on repository-local skill links or the shared matrix.

The article includes language-pivoted links to the sample README, DEPLOYMENT.md, RUNBOOK.md, and production-readiness skill for each language in `microsoft/Agents`. The sample locations are `samples/{python,nodejs,dotnet}/production-reference/`. These links target their intended locations on `main`; confirm the corresponding source changes have merged and every link resolves before publication. Track the source changes in [microsoft/Agents PR #710](https://github.com/microsoft/Agents/pull/710).

## Proposed TOC placement

Add this entry to `microsoft-365-agents/TOC.yml`:

```yaml
- name: Prepare an agent for production
  href: production-ready-agent.md
```

Proposed area: deployment and testing. The published landing page groups “Test your Agents SDK agent,” “Test your agent locally in Microsoft 365 Agents Playground,” and “Deploy your agent to Azure and register with Azure Bot Service manually” under “Deploy and test your agent.” Place this article after local testing and before deployment if that matches the actual TOC hierarchy. It supplies the controls and release criteria readers need before deploying. Exact placement, neighboring labels, and ordering require repository verification.

## Source material

Source repository: [microsoft/Agents](https://github.com/microsoft/Agents). Source change tracking: [PR #710 — Add JavaScript production readiness guide, skill, and hardened samples](https://github.com/microsoft/Agents/pull/710). Local source and sample behavior were inspected on October 9, 2026.

The inspected local source commit is `0f1d121a2f870d2f7f85569351c3bb0a5436bcf4` ("Add .NET and Python"). At the handoff update, PR #710 is open and its published head is [commit cf25c5e68c793b30fd26c98344b4b371c8f1be6c](https://github.com/microsoft/Agents/commit/cf25c5e68c793b30fd26c98344b4b371c8f1be6c), the parent of that local commit. The inspected .NET and Python additions haven't been pushed to the upstream PR, and the Learn bundle also has local changes. Before technical sign-off, ensure the complete reviewed sources are available upstream and record a permalink to the final reviewed commit. Use the PR to track ongoing work and the commit permalink to identify the exact reviewed version.

| Source | Use |
|---|---|
| `docs/python/production-ready-agent.md` | Python authentication, outbound policy, state, lifecycle, and verification. |
| `docs/nodejs/production-ready-agent.md` | Shared production workflow, Node.js APIs, conditional controls, and checklist. |
| `docs/dotnet/production-ready-agent.md` | ASP.NET authorization, identity, state wrapper, lifecycle, and verification. |
| `docs/shared/production-readiness.md` | Deployment boundary, maturity states, cross-language requirements, and evidence limits. |
| `docs/shared/readiness-matrix.md` | Control statuses, applicability, dependencies, and required evidence. |
| `samples/python/production-reference/` | README, agent construction, hosting pipeline, startup, and conditional Blob writes. |
| `samples/nodejs/production-reference/` | README, package manifest, Express pipeline, configuration, storage, and telemetry. |
| `samples/dotnet/production-reference/` | README, AppHost, and VersionedStorage implementation. |

Published primary sources checked:

- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/): terminology and published navigation grouping.
- [Configure authentication in your agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/configure-authentication-msal): canonical destination for the old JavaScript authentication URL and identity context.
- [Use storage in your agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage): provider terminology and durable storage guidance.
- [.NET SDK repository](https://github.com/microsoft/agents-for-net) and [JavaScript SDK repository](https://github.com/microsoft/agents-for-js): SDK provenance. Version-specific sample claims are based on the inspected checkout, not inferred from current main branches.
- [Python SDK repository](https://github.com/microsoft/Agents-for-python): authoritative SDK location; the web fetch failed. Confirm version-specific provider and validator behavior against the released package source before publishing.

Baseline versions are intentionally presented as reference versions, not latest-version recommendations. Python uses `1.8.0`, .NET uses `1.8.77`, and Node.js declares `^1.8.1` SDK dependencies with a lockfile.

Accuracy decisions preserved in the article:

- Python and .NET explicitly check the Web Chat channel ID and URL components. The Node.js sample's custom middleware checks the hostname; complete URL enforcement depends on the installed SDK. Don't claim identical middleware coverage.
- Python and .NET use sample-defined concurrency wrappers. These types aren't SDK APIs. The .NET wrapper requires reloading the Blob version before repeated changed saves in one turn.
- Node.js Blob access uses `DefaultAzureCredential` configured with a managed identity client ID. Don't describe this as an exclusively managed-identity credential chain without deployment evidence.
- Python and .NET filter telemetry to application instrumentation. Node.js includes SDK instrumentation and needs inspection of its exported content.
- The references don't provision the external traffic-protection layer or establish production certification.

## Assets

No images, diagrams, media directories, or includes are required. Tables and code snippets are inline. No alt text is required because the article contains no images.

## Metadata to confirm

- Replace `author: <GitHub-username>` with the owning author's GitHub username.
- Replace `ms.author: <Microsoft-alias>` with the owning Microsoft alias.
- Confirm the skill-provided `manager: kjette`, `ms.reviewer: cyanderson`, and `ms.service: microsoft-365-agents-sdk` against current docset ownership conventions.
- Confirm the publication date; the draft uses `10/09/2026`.
- Confirm the title, filename, and `how-to-guide` topic classification.

## Repository validation

1. Compare the article with nearby Learn articles for terminology, heading style, metadata, and audience expectations.
2. Confirm `dev-lang-python-nodejs-dotnet` exists in `microsoft-365-agents/zone-pivot-groups.yml` with `python`, `nodejs`, and `dotnet`. Verify the generated `agents-sdk/zone-pivot-groups.json` path and both pivot metadata fields. Reuse the approved group; don't create a new group without need.
3. Confirm TOC placement, neighboring entries, indentation, and the article path. Check whether the landing page also needs an entry under local conventions.
4. Validate external links and replace same-docset Learn URLs with verified relative paths if required by the repository. Confirm the source changes tracked by PR #710 have landed, record the final reviewed commit permalink, and verify the sample, skill, deployment, runbook, and shared-document links in `microsoft/Agents` for all three language pivots.
5. Confirm that no include or media dependencies are missing. This bundle requires neither.
6. Recheck version-specific APIs against the released SDK packages, especially Python imports and Blob extension methods, .NET AgentState concurrency behavior, and Node.js issuer/service-URL validation. Run snippets in the corresponding sample context; they aren't standalone applications.
7. Run repository Markdown, metadata, link, and build validation. Render the Learn preview for all three pivots and check section continuity, code fences, tables, persistent language selection, and next-step links.
8. Obtain technical review of the stated security boundary and evidence requirements. Local documentation checks don't replace deployed security, state, telemetry, alert, edge, or rollback verification.

Validation in the source checkout is limited to source inspection and documentation structure/link checks. No application code changed and no Azure deployment checks were performed.
