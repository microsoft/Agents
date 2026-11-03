# Teams synchronization reference inputs

These maintained production inputs supply startup guidance, the shared .NET authentication helper, and fallback Teams icons. They are independent of generated destinations and are fingerprinted in frozen migration plans. Migration sessions may only edit the selected sample, never these references.

## Provenance

Initial snapshot: southworks/Agents commit `7e367ce66552feb9c934a492615a841b40915a3f`:
- `dotnet/`: code and configuration from `samples/dotnet/quickstart` (net8.0, Agents 1.8 baseline).
- `dotnet/appManifest/`: icons from `samples/dotnet/quickstart/appManifest`.

`AspNetExtensions.cs` last changed locally in commit `0e789f5dc59670612e0e03ca450b4bff42f837c4`. Its authoritative upstream location is https://github.com/microsoft/Agents-for-net/blob/main/src/samples/Shared/AspNetExtensions.cs ; an exact upstream revision has not been established. The pinned local snapshot above is the supplied evidence, not a claim that upstream main is immutable.

Microsoft copyright and MIT licensing apply; source headers are retained. See the repository LICENSE.

## Maintenance

Refresh these files deliberately in a reviewed automation change, recording the exact source revision and compatibility validation. Do not download an unpinned helper during migration. Reference dependency versions describe the snapshot; the frozen plan selects the migration SDK and takes precedence. Do not copy reference versions over that selection. Validate affected reference builds and both language import/manifest regressions after refreshing. Missing references must be repaired before planning, rather than substituted from generated samples.
