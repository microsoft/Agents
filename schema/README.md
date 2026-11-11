# Agents configuration schema

**Preview contract: 0.1.0.** This is the Phase 1 schema artifact for
[#738](https://github.com/microsoft/Agents/issues/738), not a released or
cross-runtime-conformant configuration API. SDK-owner review and release
publication are still required before declaring a stable contract.

[`agents-configuration.schema.json`](agents-configuration.schema.json) defines a
canonical camelCase JSON document using JSON Schema Draft 2020-12. Downstream
projects own their validator choice and integration. The schema is self-contained:
its `$ref` values point inside the file, and no SDK or service is needed to validate
a document with a compatible validator.

## Scope and field inventory

At least one of `connections` or `connectionsMap` must be present. Each is optional
individually; empty collections are allowed. Connections and handlers require a
`settings` object, and each route requires `serviceUrl` and `connection`.

| Canonical path | Type / structural constraint |
| --- | --- |
| `connections` | Object keyed by nonempty connection names |
| `connections.<name>.settings` | Required object; known fields and provider extensions |
| `...settings.authType` | Nonempty string, including custom provider names |
| `...settings.tenantId`, `clientId`, `clientSecret`, `authorityEndpoint` | Optional strings; no credential-completeness or URI checks |
| `...settings.scopes` | Array of nonempty strings |
| `connectionsMap` | Ordered array of route objects |
| `connectionsMap[].serviceUrl`, `connection` | Required nonempty strings |
| `connectionsMap[].audience` | Optional nonempty string |
| `cloudAdapterOptions.emitStackTrace` | Boolean |
| `outboundHostValidator.enabled`, `includeDefaultMicrosoftHosts` | Booleans |
| `outboundHostValidator.hosts` | Array of nonempty strings |
| `agentApplication.userAuthorization.handlers` | Required object when `userAuthorization` is present; keyed by nonempty handler names |
| `...handlers.<name>.settings` | Required object; known fields and provider extensions |
| `...settings.azureBotOAuthConnectionName`, `title`, `text`, `oboConnectionName`, `invalidSignInRetryMessage` | Optional strings |
| `...settings.enableSso` | Boolean |
| `...settings.scopes`, `oboScopes` | Arrays of nonempty strings |
| `...settings.invalidSignInRetryMax` | Integer greater than or equal to zero |

All fields not explicitly required above are optional. Connection and handler names
exclude `__proto__`, `constructor`, and `prototype`, case-insensitively, following
the existing proposed schema.

The root allows unrelated application/hosting sections such as `Logging`. Modeled
core objects reject unknown properties, **except connection and handler settings**:
provider extensions may be strings, booleans, integers, or arrays of nonempty
strings. Extension objects, nulls, and fractional numbers are not supported.
An unknown setting, including a misspelled or runtime-unsupported one, can therefore
pass validation if it fits an extension type.

Initial vocabulary and constraints are adapted from the
[JavaScript proposal](https://github.com/microsoft/Agents-for-js/blob/5d802ef2ff43b0ce6d8aa6cc31206139201bef86/docs/schemas/agents-configuration.schema.json).
The basic connection and route concepts are also visible in
[.NET connection binding](https://github.com/microsoft/Agents-for-net/blob/bf5d48bda813c27c06a07e7c7ed6a7ca176bb825/src/libraries/Core/Microsoft.Agents.Authentication/ConnectionSettingsBase.cs)
and [routing types](https://github.com/microsoft/Agents-for-net/blob/bf5d48bda813c27c06a07e7c7ed6a7ca176bb825/src/libraries/Core/Microsoft.Agents.Authentication/Model/ConnectionMapItem.cs).
These sources establish the starting vocabulary, not identical behavior or support
in all SDKs. This table is a bounded schema inventory, not a capability manifest.

## Examples and validation boundaries

- [`examples/minimal.json`](examples/minimal.json) shows a named connection and
  default route. It deliberately omits credentials required for deployment.
- [`examples/host-and-authorization.json`](examples/host-and-authorization.json)
  includes host options, user authorization, and application-owned logging.

**Schema validity establishes structure only.** It does not establish:

- Whether a particular SDK supports a field, authentication method, provider
  setting, handler, or representation.
- Environment-variable mappings, source precedence, alias resolution, string
  coercion, defaults, interpolation, or secret resolution.
- Existence/casing of referenced connections, route matching, URI/hostname
  correctness, network reachability, or authentication success.

The schema neither mutates documents nor applies defaults. `clientSecret` has a
`writeOnly` annotation, but validators do not automatically redact errors. Never
print configuration documents, invalid values, or unfiltered validator diagnostics
containing secrets; report safe paths and error keywords instead. Example values
are synthetic, and `${...}` is not expanded by this contract.

Native SDK configuration is **not automatically canonical**. .NET samples use
PascalCase appsettings and can contain null secret placeholders; Python's
[environment loader](https://github.com/microsoft/Agents-for-python/blob/5f3572bc0fa7b0858e88e0a39c5c898bc9503876/libraries/microsoft-agents-activity/microsoft_agents/activity/config/_load_configuration.py)
produces uppercase keys. This schema does not normalize those formats or promise
that the canonical document loads unchanged in every runtime.

The recognized-root requirement rejects documents containing only unrelated or
native-cased sections, but does not make root property names case-insensitive.
A mixed document such as `{"connections": {}, "Connections": {...}}` can pass:
the PascalCase section is unmodeled root data. Do not present such a pass as
validation of the native section.

Certificates, sidecar settings, timeout representations, legacy aliases,
assembly/type metadata, runtime-specific handler discriminators, and
default-handler/auto-sign-in behavior are not explicitly modeled. Some may fit a
provider extension type; acceptance does not establish support.

## Consume a pinned artifact

Before the first release, use the file from a reviewed, revision-pinned checkout.
This preview has no `$id`: it does not claim a hosted release URL exists.

Once the schema is merged or released, pin the full reviewed commit SHA, not
`main` or a mutable tag. For example, from PowerShell:

```powershell
$contractCommit = '<reviewed-40-character-commit-sha>'
$schemaFile = Join-Path $PWD "agents-configuration.$contractCommit.schema.json"
$schemaUrl = "https://raw.githubusercontent.com/microsoft/Agents/$contractCommit/schema/agents-configuration.schema.json"
Invoke-WebRequest -Uri $schemaUrl -OutFile $schemaFile -ErrorAction Stop
```

Cache that exact file as a build input. Configure your Draft 2020-12 validator to
load it locally and fail if retrieval, schema compilation, or document validation
fails. Do not fall back to skipped validation or a newer schema on an error.
Compatible tools can also associate the pinned schema URL/file with a JSON
document for editor completion; that association does not add runtime support.
There is no package to install or service to call to obtain the contract itself.

Downstream SDK loader integration, per-language validation commands, and mandatory
downstream build/startup checks are outside Phase 1.

## Contribute and test

The schema test toolchain is isolated in this folder so restoring it does not
restore the larger root/sample development toolchain. It has one direct
development dependency, Ajv, and its own npm lockfile.

From the repository root in PowerShell:

```powershell
npm --prefix schema ci --ignore-scripts
if ($LASTEXITCODE -ne 0) { throw 'Schema test dependencies could not be restored.' }
npm --prefix schema test
```

npm uses your configured registry and authentication settings. These dependencies
are public packages; no project-specific private feed or token is required.
The lockfile pins versions and integrity hashes without registry-specific
`resolved` URLs. The local `.npmrc` sets `omit-lockfile-registry-resolved=true`
to preserve this when regenerating the lockfile.

Tests validate the meta-schema, compile strictly, reject external `$ref` values,
validate examples and positive cases, check negative paths/keywords, and verify
that successful validation does not modify input. The cases intentionally test
known limits such as unresolved references and unmodeled root/provider data.
Diagnostics emitted by this harness include paths and keywords, not document
values.

`.github/workflows/validate-configuration-schema.yml` installs only these test
dependencies and runs the same checks without repository secrets, including on
fork pull requests.

## Versioning and publication

Obtain review from the affected SDK maintainers before promoting a preview
vocabulary to a stable contract.

Version the contract independently of SDK packages:

| Change | Version change |
| --- | --- |
| Documentation/annotation correction with unchanged accepted instances | Patch |
| Additive optional vocabulary without rejecting previously valid documents | Minor |
| Removed/renamed fields, narrower accepted values, changed requiredness, or other changes that reject previously valid documents | Major |

An optional field that replaces a previously permissive provider/root extension
can still tighten validation; evaluate the actual accepted instances before
calling it a minor change. Runtime defaults are not part of this initial contract.

Maintainer release procedure:

1. Obtain schema/SDK-owner review and choose the independent contract version.
2. Update the preview/version annotation and this README. Before the release
   commit, set `$id` to
   `https://raw.githubusercontent.com/microsoft/Agents/configuration-v<version>/schema/agents-configuration.schema.json`.
3. Run the schema tests, then publish through the maintainers' normal reviewed
   commit/release process using an annotated `configuration-v<version>` tag.
4. Record the full resolved commit SHA in the release notes. Treat tags as
   discoverability labels; consumers should pin the immutable SHA.
5. Retrieve the artifact by SHA, and separately through its `$id`. Compare both
   files byte-for-byte with the schema in the tagged checkout. Treat any mismatch
   or fetch failure as a failed publication.
6. Run the existing fixtures against the retrieved file, then announce the pinned
   URL and compatibility notes. Do not declare publication complete before these
   checks pass.

For step 6, the test harness accepts a local artifact override:

```powershell
$env:AGENTS_CONFIGURATION_SCHEMA = (Resolve-Path $schemaFile).Path
try {
    npm --prefix schema test
    if ($LASTEXITCODE -ne 0) { throw 'Published schema failed the shared schema fixtures.' }
} finally {
    Remove-Item Env:AGENTS_CONFIGURATION_SCHEMA
}
```

No release, hosted `$id`, downstream adoption, or SDK-owner approval is implied by
this preview PR. The existing JavaScript proposal must not become a second
independent source of truth; consumption of the central version and retirement or
redirection of that proposal remain downstream follow-up work.

## Deferred Phase 2

Capability manifests, real-loader behavioral fixtures/adapters, cross-repository
conformance workflows, warning-to-error rollout, and optional generated runtime
schemas remain in #738 and its child issues:
[microsoft/Agents-for-js#1305](https://github.com/microsoft/Agents-for-js/issues/1305),
[microsoft/Agents-for-net#1026](https://github.com/microsoft/Agents-for-net/issues/1026),
and [microsoft/Agents-for-python#602](https://github.com/microsoft/Agents-for-python/issues/602).
