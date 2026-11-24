# Generated Teams samples

Synchronization owns maintenance of generated Teams samples in every language.
The language profiles in `automation/teams-sample-sync/config/targets.yml` define
the managed destination roots, currently `samples/dotnet/teams/**` and
`samples/nodejs/teams/**`. This rule also applies to future language profiles.
Outside that process, do not change any files in those roots, including implementations,
project/package versions, lockfiles, shared helpers such as `AspNetExtensions.cs`, tests,
manifests, configuration, assets, documentation, formatting, cleanup, or dependency updates.

The authorized maintenance path includes the synchronization runtime and Copilot tasks
assigned through workflow-generated handoff issues with prepared patches and migration
evidence. Assignment to an arbitrary issue does not grant this exception. Follow the
synchronization instructions and validation requirements for the selected samples.

If an ordinary task needs a change or encounters a build failure in these roots, report
the problem and identify the synchronization work needed. Reference an existing issue
when appropriate; do not automatically open an issue or trigger a workflow.

Review generated samples normally: flag real defects, unauthorized changes, and missing
or failing validation. Recommend fixing them through synchronization, including required
SDK compatibility migrations, rather than manual generated-output patches.

Automation source and the shared contract harness outside these roots remain maintainable
through ordinary tasks. This ownership guidance does not replace CI enforcement.
