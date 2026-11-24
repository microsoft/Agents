# Teams synchronization reference inputs

These maintained production inputs supply startup guidance, the shared .NET authentication helper, and fallback Teams icons. They are independent of generated destinations and are fingerprinted in frozen migration plans. Migration sessions may only edit the selected sample, never these references.

## Provenance

- `dotnet/`: code and configuration from `samples/dotnet/quickstart` (net8.0, Agents 1.8 baseline).
- `nodejs/`: startup code and TypeScript settings from `samples/nodejs/quickstart` (Agents 1.9.1 baseline); playground tooling and lifecycle installs are omitted.
- `dotnet/appManifest/`: icons from `samples/dotnet/quickstart/appManifest`.
- `manifest-assets/nodejs/`: icons from `samples/nodejs/proactive/appManifest`.
- `python/`: startup code, dependency declarations and environment template from `samples/python/quickstart` at repository revision `93be4925735518cba1e63592542c388576253a37`. This hosting snapshot includes the older `AgentApplication.adapter` pattern. Use the [current migration skill](../../../agent-plugins/agents-for-python/skills/teams-sdk-to-agents-sdk-python-migration/SKILL.md#application-construction-and-hosting) for routing and transport and the frozen SDK selection for dependency versions.
- `manifest-assets/python/`: icons from `samples/python/proactive/appManifest` at repository revision `93be4925735518cba1e63592542c388576253a37`.

`AspNetExtensions.cs` last changed locally in commit `0e789f5dc59670612e0e03ca450b4bff42f837c4`. Its authoritative upstream location is https://github.com/microsoft/Agents-for-net/blob/main/src/samples/Shared/AspNetExtensions.cs ; an exact upstream revision has not been established.

Microsoft copyright and MIT licensing apply; source headers are retained. See the repository LICENSE.

## Maintenance

Refresh these files deliberately in a reviewed automation change, recording the exact source revision and compatibility validation. Do not download an unpinned helper during migration. Reference dependency versions describe the snapshot; the frozen plan selects the migration SDK and takes precedence. Do not copy reference versions over that selection. Validate affected reference builds and all affected language import/manifest regressions after refreshing. Missing references must be repaired before planning, rather than substituted from generated samples.
