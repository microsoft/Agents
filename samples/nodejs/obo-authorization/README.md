# OBO Auth

This Agent uses the [Microsoft 365 Agents SDK for JavaScript](https://github.com/microsoft/Agents-for-js) to authorize users with Azure Bot Service and call Microsoft Graph on their behalf.

- Azure Bot Service obtains an initial user token for the sample's exposed API.
- The SDK automatically exchanges that token for a downstream Graph token using On-Behalf-Of (OBO). The agent uses it to request the signed-in user's profile.

## Prerequisites

- [Node.js](https://nodejs.org) 22 or greater (the locked dependency tree requires Node 22), with npm.
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for local development.
- Access to an Azure subscription to configure an Azure Bot, permission to create app registrations and secrets, and a work or school account in the same tenant. An authorized administrator must grant consent for the administrator-approved setup below.

## Configure Azure Bot Service

These instructions use one tenant, client secrets, public-cloud nonregional Azure Bot Service, and **Test in Web Chat**. Teams SSO and federated credentials require separate configuration; see Further reading.

1. Create the two single-tenant app registrations used by this sample.

   | Identity | Purpose | Environment prefix |
   | --- | --- | --- |
   | Bot app | Authenticates channel traffic with Azure Bot Service | `connections__serviceConnection__settings__` |
   | OAuth/OBO app | OAuth client, exposed API resource, and SDK OBO client | `connections__oboConnection__settings__` |

   In each registration's **Overview**, record **Application (client) ID** and **Directory (tenant) ID**. Under **Certificates & secrets**, create a client secret and securely record its **Value** when shown. The secret ID is not the secret value. Keep the two registrations' credentials separate.

1. [Create an Azure Bot](https://learn.microsoft.com/microsoft-365/agents-sdk/provision-azure-bot-service-manually) configured as **Single Tenant** with client-secret authentication. Its Microsoft App ID and tenant must match the **Bot app**. See [JavaScript channel authentication](https://learn.microsoft.com/microsoft-365/agents-sdk/azure-bot-authentication-for-javascript) for other credential types.

1. Configure the **OAuth/OBO app** in Microsoft Entra ID.

   - Under **Authentication**, add a **Web** redirect URI: `https://token.botframework.com/.auth/web/redirect`. Use the [official redirect table](https://learn.microsoft.com/azure/bot-service/ref-oauth-redirect-urls) for regional or sovereign environments.
   - Under **Expose an API**, set the Application ID URI to `api://<OBO_APP_ID>`. Add an enabled delegated scope named `defaultScopes`. Supply an admin consent display name (for example, “Access the OBO sample”) and description (“Allow the sample to act on behalf of the signed-in user”). Choose who may consent according to tenant policy; add user consent labels if allowing users to consent.
   - Under **API permissions**, add **Microsoft Graph → Delegated permissions → User.Read**. This allows Graph `/me`; application permissions are not suitable for this user flow.
   - Also use **Add a permission → My APIs** to select this same OAuth/OBO app's exposed delegated `defaultScopes` permission. For this administrator-approved baseline, have an authorized administrator **Grant admin consent** for the listed permissions in this tenant, including the exposed scope and Graph `User.Read`. Verify the granted status before testing.
   - Consent to the exposed scope alone does not establish Graph consent. OBO cannot show its own interactive consent prompt, and `.default` does not grant permissions automatically. User consent alternatives depend on tenant policy and must establish downstream Graph consent as well. See [OBO consent requirements](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow).

1. On the **Azure Bot**, open **Configuration → OAuth Connection Settings → Add Setting**.

   | Azure field | Value |
   | --- | --- |
   | Name | `OboAutoConnection` (or your chosen name, matching the environment setting exactly) |
   | Service Provider | **Azure Active Directory v2** (the Microsoft Entra v2 client-secret provider) |
   | Client id | **OAuth/OBO app** Application (client) ID |
   | Client secret | **OAuth/OBO app** secret **value** |
   | Tenant ID | **OAuth/OBO app** Directory (tenant) ID |
   | Scopes | `api://<OBO_APP_ID>/defaultScopes` |
   | Token Exchange URL | Leave blank for this Web Chat recipe; this is an SSO setting, not a Graph scope or endpoint |

   **Save**, then **Test Connection** and sign in with an account in the tenant. This verifies initial OAuth, not downstream Graph access through OBO. See [OAuth connection fields](https://learn.microsoft.com/azure/bot-service/bot-builder-authentication).

   The initial scope targets the exposed API, not Graph. Do not combine Graph resource scopes with this API scope. Standard sign-in scopes such as `openid`, `profile`, and `offline_access` are separate from resource scopes. This recipe uses the same registration for OAuth client, API resource, and OBO client: SDK 1.9.1 checks that the initial token's audience contains its `azp` (or `appid`) client ID. An `api://` prefix alone is insufficient. A token issued for Graph cannot serve as this app's OBO assertion.

1. Configure the sample locally, from **`samples/nodejs/obo-authorization/`**.

   ```bash
   npm ci --ignore-scripts
   cp env.TEMPLATE .env
   ```

   In PowerShell, copy with:

   ```powershell
   Copy-Item env.TEMPLATE .env
   ```

   Fill all six credential keys in `.env`:

   | Key | Owner / value |
   | --- | --- |
   | `connections__serviceConnection__settings__clientId` | Bot app client ID |
   | `connections__serviceConnection__settings__clientSecret` | Bot app secret value |
   | `connections__serviceConnection__settings__tenantId` | Bot app tenant ID |
   | `connections__oboConnection__settings__clientId` | OAuth/OBO app client ID |
   | `connections__oboConnection__settings__clientSecret` | OAuth/OBO app secret value |
   | `connections__oboConnection__settings__tenantId` | OAuth/OBO app tenant ID |

   Quote values containing `#`. `.env`, dependencies, and generated `dist/` output are ignored by Git; never commit secrets or bearer tokens.

   Configure the existing `graph` handler:

   ```dotenv
   AgentApplication__UserAuthorization__Handlers__graph__Settings__azureBotOAuthConnectionName=OboAutoConnection
   AgentApplication__UserAuthorization__Handlers__graph__Settings__oboConnectionName=oboConnection
   AgentApplication__UserAuthorization__Handlers__graph__Settings__oboScopes=https://graph.microsoft.com/.default
   ```

   The **Azure OAuth connection name** selects saved settings on the Azure Bot. **`oboConnection`** selects SDK credentials in this process; it is not a second Azure OAuth setting. **`graph`** is the handler ID used by routes, token calls, and the environment-key segment. The wildcard channel mapping stays on `serviceConnection`; OBO selects its credentials by name and needs no `serviceUrl=obo` mapping.

   With `oboScopes` configured, `getToken(context, 'graph')` returns the downstream Graph token. Do not exchange that returned token again. For manual OBO as an alternative, omit `oboScopes` and call `exchangeToken(context, 'graph', { scopes: ['https://graph.microsoft.com/.default'] })` to obtain the downstream token. This sample runs only the automatic path; there is no manual command or second handler.

1. Start the agent:

   ```bash
   npm start --ignore-scripts
   ```

   The explicit `start` script builds using the locally declared TypeScript compiler, then runs `node --env-file .env dist/index.js`. Node loads `.env`; already-set process variables take precedence. `npm run build` builds separately. Restart after configuration changes.

1. In a second terminal, run:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

   Keep both the agent and tunnel running. Set the Azure Bot **Messaging endpoint** to `<TUNNEL_HTTPS_URL>/api/messages`, then open **Test in Web Chat** in the Azure Portal.

## Interacting with the Agent

- At conversation start, the agent sends “Welcome to the OBO Auth App demo!” and checks token availability. It does not request a profile at this point.
- Send an ordinary message to trigger the protected `graph` handler and sign in. On success, the agent echoes `You said.<message>` and displays `Your Graph profile: <displayName>` from a real Graph `/me` request.
- Send `/status` to repeat the welcome and token-availability check. It can acquire, refresh, or exchange a token; it is not a passive cache inspection and does not call Graph `/me`.
- Send `/logout` to sign out the configured `graph` handler, without first acquiring an OBO token or calling Graph. Success displays `user logged out`. Sign-out does not clear the Entra browser session, so signing in again may not ask for credentials.
- While sign-in is waiting for a verification code, the SDK consumes messages, including `/status` and `/logout`. Send the exact text `CancelledByUser`, then repeat the desired command. Closing the sign-in browser alone may leave this flow active. Cancellation and signing out are distinct.
- A Graph HTTP, invalid-response, or network failure is reported separately from a missing token; it does not by itself mean the user is signed out.

## Troubleshooting

- **Install/build/start fails:** use Node 22+, run `npm ci --ignore-scripts` in the sample directory, and copy/fill `.env`. The compiler is local; no global TypeScript installation is required. If port 3978 is occupied, stop the conflicting process or set `PORT` and update the tunnel accordingly.
- **No messages arrive:** keep the tunnel running and verify the current HTTPS endpoint, including `/api/messages`. Channel authentication failures (often HTTP 401) require checking the Bot app credentials in `serviceConnection` against the Azure Bot.
- **Initial OAuth fails:** check the exact saved connection name, Web redirect URI for your environment, tenant/account, secret value and expiry, and exposed-scope consent. Test Connection isolates initial OAuth only.
- **Token is not exchangeable:** ensure initial OAuth requests `api://<OBO_APP_ID>/defaultScopes`, not Graph, and uses the same OAuth/OBO app as client and resource. Check the SDK audience/client requirement above without logging or sharing bearer tokens.
- **OBO fails:** check `oboConnection` credentials, expiry, delegated Graph `User.Read`, and downstream admin consent. Re-authorize after correcting permissions when necessary; `.default` requests granted permissions rather than granting them.
- **Graph fails after acquisition:** HTTP 401 indicates a rejected token; HTTP 403 can indicate insufficient access. Check permission/consent and Graph availability. Network/timeout and invalid-response messages concern the profile request, not proof of sign-out. Do not paste secrets, tokens, or raw sensitive errors into issues.
- **Commands appear ignored during sign-in:** send `CancelledByUser`, then repeat `/logout` or your message. Restart after editing `.env`; sign out and authorize again if cached authorization is stale.
- **Teams still prompts:** Teams SSO requires additional app, manifest, and token-exchange configuration outside this Web Chat walkthrough. Basic scopes do not guarantee prompt-free SSO.

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/Agents) and [JavaScript SDK](https://github.com/microsoft/Agents-for-js)
- [Agents SDK OAuth settings and token APIs](https://learn.microsoft.com/microsoft-365/agents-sdk/agent-oauth-configuration)
- [Microsoft identity platform OBO](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow)
- [Teams bot SSO setup (separate configuration)](https://learn.microsoft.com/microsoftteams/platform/bots/how-to/authentication/bot-sso-register-aad)
- [Federated user authorization (alternative credentials)](https://learn.microsoft.com/microsoft-365/agents-sdk/azure-bot-user-authorization-federated-credentials)
