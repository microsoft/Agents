# Repository installation scope evidence

The seven Python synchronization targets must preserve the intended sample
capabilities across languages. Existing same-family .NET and JS manifests provide
installation-scope evidence for this parity requirement, even when upstream Python
contains no manifest. This is repository evidence, not a default for unrelated apps.

Both reference manifests agree at automation baseline
`122ad7abebc6b357286dab3a5e06c21374a033c3`:

| Sample | Existing `bots[0].scopes` |
| --- | --- |
| bot-cards | personal, team, groupChat |
| bot-ai-messages | personal |
| bot-message-extensions | personal, team, groupChat |
| bot-task-modules | personal |
| bot-attachments | personal |
| bot-meetings | personal, team, groupChat |
| agent-targeted-messages | personal, team, groupChat |

Inspect the actual files at the execution base before using this evidence:

- `samples/dotnet/teams/<sample>/manifest/manifest.json`
- `samples/nodejs/teams/<sample>/manifest/manifest.json`

Record their JSON path and values in the frozen migration plan. For the configured
Python targets, use the agreed scopes unless a stronger explicit requirement or
a demonstrated Python platform limitation conflicts. Report a real conflict with
its evidence; absence of an upstream Python manifest is not itself a conflict.

Bot installation scopes and extension command contexts are different fields.
Reconcile `composeExtensions[].commands[].context` separately for message extensions.
Keep Graph/RSC permissions, domains and command discovery justified by their own
source and feature evidence; this table does not authorize unrelated permissions.
