# Authentication, permissions, and domains

Primary sources:

- [Enable SSO for bots and message extensions](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/authentication/bot-sso-manifest)
- [Resource-specific consent](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/rsc/grant-resource-specific-consent)
- [Permissions in Teams apps](https://learn.microsoft.com/en-us/microsoftteams/platform/graph-api/app-permissions/teams-app-permissions)
- [Teams CLI RSC commands](https://microsoft.github.io/teams-sdk/cli/commands/app/rsc-set/)

## Keep identity concepts separate

- `id` at manifest root identifies the Teams app package.
- `bots[].botId` identifies the bot registration used for messaging.
- `webApplicationInfo.id` identifies the Microsoft Entra app registration used for SSO, token acquisition, or RSC mapping.

These IDs can be equal, but equality is not universal. Preserve repository conventions and require a decision when registrations are distinct.

## SSO

SSO requires `webApplicationInfo.id` and `webApplicationInfo.resource`. Bot Framework token exchange can also require `token.botframework.com` in `validDomains`.

Do not generate SSO configuration only because the application has authentication for inbound bot requests. Look for user-token acquisition, OAuth routes, Graph calls on behalf of the user, an existing Entra configuration, or explicit SSO requirements.

Do not invent the application ID URI. Report it as `needs-input` when it is not documented.

## Permission classes

Classify every permission before changing the manifest:

| Permission class | Configuration owner |
|---|---|
| Teams manifest basic permissions | `permissions` in the Teams app manifest |
| Resource-specific consent | `authorization.permissions.resourceSpecific` in the Teams app manifest |
| Microsoft Graph delegated/application permissions | Microsoft Entra app registration and consent |
| Bot channel and event registration | Azure Bot, Teams CLI, or Developer Portal |

Do not move a permission between classes because its name appears similar.

## RSC

For manifest 1.12 or later, RSC uses `authorization.permissions.resourceSpecific`. Each entry requires an official permission name and `Application` or `Delegated` type. Request least privilege.

For RSC-only use, `webApplicationInfo.resource` is required but has no RSC operation. Use a stable repository-approved nonempty marker and document it. For SSO, the same property is the real application ID URI; never substitute an RSC marker.

The JSON Schema can validate the RSC entry shape without proving that a permission name exists. Verify names and types against official RSC or feature documentation and use platform validation when available.

Permission-bearing changes always require explicit intent or conclusive product requirements. Code evidence can identify a likely need, but it cannot authorize access.

Validate RSC structure against the exact released schema. Schema validation does not prove that a permission is supported, necessary, or least privilege.

Verify RSC permission names and types against current official RSC documentation. Teams CLI RSC commands can compare or configure permissions for an existing registered Teams app and accept catalog permission names, but they are remote app-management operations, not a local semantic validator. Do not run a mutating RSC command only to validate a manifest. With explicit authorization and a test tenant, install the app and exercise the consented operation as the final functional check.

## Domains

Add a domain only for a documented manifest use such as hosted tabs, URL dialogs, SSO/token exchange, or other feature-specific requirements.

- Store hosts only, not full URLs.
- Do not add schemes, paths, ports, or query strings.
- Avoid wildcards unless official feature guidance permits them and ownership is clear.
- Keep message-extension link-handler domains separate from `validDomains` unless both uses are proven.
- A messaging endpoint does not automatically require its host in `validDomains`.
