# Schema, packaging, and validation

Primary sources:

- [Microsoft 365 app manifest schema](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/)
- [Create a Teams app package](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/apps-package)

## Schema policy

- Use only a generally available schema.
- Preserve the repository-approved version when it supports all required capabilities and distribution rules.
- For new Teams agent samples in this repository, use `1.22` as the baseline unless a capability or distribution rule requires a higher released version.
- Outside this repository, use the maintained project's baseline or request a version decision. Do not select an old schema only because it is technically sufficient.
- Keep the version embedded in the `$schema` URL equal to `manifestVersion`.
- Record the version choice in the final evidence report and in a persisted evidence file when one is requested.
- Do not upgrade to the latest schema as routine cleanup.

The base template contains a `{{MANIFEST_VERSION}}` authoring placeholder. Replace it with a concrete released version before validation or packaging.

## Optional defaults and reusable inputs

For Generate mode, omit an optional property when the intended value equals the default in the exact released schema. For example, omit `supportsFiles` when false and emit it when verified file-consent support requires true. Emit an explicit default only when released feature documentation or repository convention requires it.

For Complete mode, preserve an existing explicit default unless changing it is required. Do not create formatting-only or default-only churn.

Resolve reusable values and package assets in this order:

1. Existing repository value or asset.
2. Caller-provided repository policy or approved reusable asset.
3. Safe documented default that does not change security, permissions, exposure, or product intent.
4. Conclusive source evidence.
5. `needs-input`.

For a repository sample with no stated distribution target, use local/sample authoring defaults for required non-security metadata when the repository has no more specific convention. Use clearly non-production HTTPS URLs such as `https://example.com`, record them for replacement before distribution, and continue generation. For IDs and endpoints, prefer the repository's environment placeholder convention. Do not substitute a fake production identity or endpoint.

Icons are required package files, but they do not always require user input. Reuse existing icons in the target first. Otherwise, reuse repository-approved base icons for local and sample packages when available. Mark generic icons as placeholders. For organizational or Store distribution, request approved branding when no approved icons exist. Never replace existing branded icons during Complete mode without explicit direction.

Check that every referenced local asset exists before reporting it as missing or blocking package validation. Do not infer that an icon is missing from its generic name or branding status.

## Environment placeholders

Preserve the target repository's documented placeholder convention. Microsoft 365 Agents Toolkit commonly resolves values such as `${{TEAMS_APP_ID}}` from environment files before validation and packaging.

Every placeholder must have:

- A documented owner and meaning.
- A source environment value or an explicit unresolved decision.
- Consistent use for Teams app, bot, and Entra IDs.

Do not mix `${{BOT_ID}}`, `${{AAD_APP_CLIENT_ID}}`, and other names without mapping their identities.

The `{{...}}` values in the base asset are authoring markers owned by this skill, not Agents Toolkit environment variables. Replace or remove all of them when generating a manifest. Preserve `${{...}}` only when the target repository has a documented Agents Toolkit substitution process for that value.

## Validation layers

1. **JSON:** parse the rendered manifest; reject comments, trailing commas, duplicate keys, and unresolved authoring placeholders.
2. **Schema:** validate the rendered manifest against its exact released schema.
3. **Assets and package:** verify every referenced file exists, build the ZIP, and confirm `manifest.json`, the color icon, and the outline icon are at the ZIP root with no unrelated files.
4. **Evidence:** compare every detected capability with the capability plan and generated fields.
5. **External configuration:** verify that required Entra, bot registration, RSC consent, domains, and event subscriptions are documented.
6. **Functional:** with explicit authorization, sideload into a test tenant and exercise the feature when credentials and tenant access are available.

Do not run, install, request, or recommend Agents Toolkit for validation. Its availability does not block manifest creation or this validation workflow.

Summarize the completed checks in one short paragraph in the final report. Do not expose commands, a validation matrix, internal statuses, or unavailable optional tools. If a check still fails because it needs user input, explain the failed check and its effect under **Needs user input**.

## Validation limitations

Schema and package validation do not inspect application code. They cannot prove that:

- A required capability is declared.
- Declared scopes match runtime behavior.
- RSC or Graph permissions are least privilege.
- Dialog routes and domains match.
- Message-extension command IDs match handlers.
- External portal or Entra configuration exists.

Treat these local checks as a required final gate, not as the capability decision engine.
