# S2S Support for Copilot Studio Client

*Private Preview — This feature is Private Preview, for more information about it's use, see the [Private Preview Terms](https://go.microsoft.com/fwlink/?linkid=2173149)*

## Onboarding & Hello World

Audience: Engineers in Customers/Partners onboarding to S2S feature set for Copilot Studio 

Use this guide to: understand the feature, complete Day 0 setup, and run a Hello World turn against a published agent to establish the connection. 

This feature is enabled on a per environment or tenant basis.

### Feature Overview 

S2S for Direct-to-Engine (D2E) - commonly known as the Copilot Studio Client, enables programmatic access to published Copilot Studio custom-engine agents directly via the D2E API — without going through Azure Bot Service or DirectLine. Authentication is Entra-only (AAD JWT). 

The flow has three key steps: 

1. A Tenant Admin grants the application identity in the Azure Portal delegated or application permissions to CopilotStudio.Copilots.Invoke.This permission manages the access to the Copilot Studio agent itself, and in the principle of least privilege, the app identity needs to also have access to any other access that the agent requires e.g. Graph. 

2. Maker in Copilot Studio grants permissions to the app identity. The agent maker shares the agent within the Copilot Studio sharing pane in the UX with the calling application using the Share API with viewer permissions.  

3. ACL enforcement at runtime. When the app identity calls D2E, the system validates that (a) the app was shared with the agent, (b) it has the CopilotStudio.Copilots.Invoke permission, and (c) the caller's tenant matches the agent's tenant. 

## Two Integration Modes

| Mode | Token type | User context | When to use |
|------|-----------|--------------|-------------|
| True S2S (application context) | App-only token via client credentials flow | None | Backend-to-backend, dispatcher/broker, anonymous agents. Requires Copilot Studio agent to be 'No Authentication' mode. |
| S2S for user-delegated flows — verifying and enforcing the user and application identity has access to the agent | Delegated token (OBO) | Available | App calls on behalf of a user; authenticated agents. |

<br>

Access rule for OBO: Access is granted only when the application broker has at least viewer permission on the agent and the user also has access (owner / viewer / editor). 

## Key Concepts

### Copilot Studio Client (D2E) and Direct Line


| Aspect | DirectLine | Direct-to-Engine (D2E) |
|--------|-----------|------------------------|
| Path | Client → Azure Bot Service → MCS | Client → PPAPI Gateway → MCS |
| Auth | DirectLine secret/token (custom) | Entra (AAD) JWT only |
| Protocol | Activity Protocol | Activity Protocol |
| Latency | Higher (extra ABS hop) | Lower (no ABS hop) |
| S2S support | App-only via custom flow | Native, via Entra app-only or OBO |

<br>

- *PPAPI*: Front door that validates the JWT before requests reach the D2E controller. All downstream codes trust the gateway's validated claims. 

- *Activity protocol*: JSON payload shape D2E uses (Used by the M365 Agents SDK and Copilot Studio) 

- *Copilot Studio Client / Agents SDK*: .NET, JavaScript, and Python client libraries that wrap D2E.  

- *CopilotStudio.Copilots.Invoke:* The Entra API permission your app registration needs. Comes in Application (app-only) and Delegated (OBO) options. 

- *Anonymous vs. authenticated agent*: Set on the agent in MCS Settings > Security > Authentication. S2S (Application) only works against anonymous agents. 

- *Integrated Authentication mode*: Required on the agent for delegated/OBO S2S to work end-to-end. 

- *Sharing*: A Copilot Studio action, the maker shares the agent with the app identity.  

- *SSE streaming*. Set Accept: text/event-stream to receive streamed turn responses; otherwise you get one JSON response. 

## Constraints

- Same-tenant only. The calling application must be registered in the same tenant as the agent. Cross-tenant integration is not supported today. This is regardless of if the application registration is configured to support multi-tenant and multi-tenant in Copilot Studio is not supported. " I have not tested the cross-tenant integration. So is this just for diligence until we run tests and identify gaps.  

- True S2S requires anonymous mode. In application context there is no user, so user-auth-enabled tools and connections are not supported in this flow. The agent must have no end-user auth configured. In Copilot Studio this means ‘No Authentication’ is set. See Section 12. Security Best Practice Context in this document. 

- User-delegated S2S requires integrated auth mode. D2E APIs only support Entra auth and cannot support OAuth or Custom AAD via REST. Works with agent in any auth mode (No auth, Integrated and Custom). D2E APIs only support Entra auth and the Custom AAD auth is best effort

## Existing S2S Allow Listed Tenants

Existing EnableS2SAuthForD2E customers. Customers currently allowlisted under the legacy EnableS2SAuthForD2E feature continue to be supported. They will be transitioned onto the share-authorization-based ACL model. 

## Security Guardrails

Three identity-consistency checks prevent conversation hijacking across turns: 

| Check | What it does | Failure mode |
|-------|-------------|--------------|
| App identity consistency | For app-initiated conversations, only the same app (same ObjectId) can continue the conversation across all turns. | CallerIdentityMismatch |
| User identity consistency | For user-delegated conversations, only the same user can continue the conversation across all turns. | CallerIdentityMismatch |
| Identity type consistency | A conversation started by an app-only identity cannot be continued by a user-delegated identity, and vice versa. | CallerIdentityTypeMismatch |


These run on top of the existing PPAPI gateway JWT validation, the per-agent share ACL, and the immutable ConversationInfo record (UserId, ChannelId, ConversationId, UserTenantId cannot be modified after the conversation is created). Additional error codes will be added for visibility.

## Connect to your Copilot Studio Agent with S2S - Hello World

### Step 1 - Copilot Studio Pre-configuration

1. Open your environment (not production – private preview support only)

2. Ensure the feature flag has been enabled (you need to ask Microsoft to enable this)  

3. Create an agent in Copilot Studio  

4. Create your App Identity based on delegated or application scenarios outlined above. 

### Step 2 — Creating your own app registration (you need elevated permissions in Azure) 

1. Visit Azure Portal — App registrations and create a new App registration. 

2. Navigate to API permissions and click “Add a permission”. 

3. In APIs my organization uses, search for Power Platform API. 

4. Add Power Platform API → Delegated permissions and Application permissions → CopilotStudio.Copilots.Invoke. 

5. Get the permissions admin-consented by your tenant admin. 

6. Generate a client secret (or wire up a federated credential / managed identity). 

### Step 3 — Share the agent with the app identity 

1. Open your agent in Copilot Studio

2.  Click the extended menu at the top right of the window ‘…’  and click Share. 

3. Search and select your app identity. 

4. Click Share / Update.  

5. Reopen the panel to confirm the app appears in the shared list 

### Step 4 - Configure the Copilot Studio Client

1. Use the [Microsoft 365 Agents SDK / Copilot Studio Client](https://learn.microsoft.com/en-us/microsoft-copilot-studio/publication-integrate-web-or-native-app-m365-agents-sdk?tabs=dotnet#configure-your-app-registration-for-service-principal) and configure the properties in the configuration settings, as shown below:

#### Sample Client

```
"CopilotStudioClientSettings": { 

  "DirectConnectUrl": "",          // Connection string from the Native/Web app channel for the agent 

  "EnvironmentId": "",             // From the URL of your MCS agent or Settings / Advanced / Metadata 

  "SchemaName": "",                // Settings / Advanced / Metadata 

  "TenantId": "",                  // Settings / Advanced / Metadata 

  "UseS2SConnection": false,       // true = application context, false = user-delegated (OBO) 

  "AppClientId": "",               // Entra ID > App registrations > (your app) > Application (client) ID 

  "AppClientSecret": "",           // App registration > Certificates & secrets (only for true S2S) 

  "EnableDiagnostics": true,       // include trace + debug logs on the console 

  "UseExperimentalEndpoint": false,// true = use the direct island URL, false = PPAPI URL (island-agnostic) 

  "Cloud": "Preprod"               // "Test", "Preprod", or "Prod" 

} 
```

##### Configuration rules 

- TenantId is required. 

- Provide either DirectConnectUrl or the trio EnvironmentId + SchemaName + TenantId. 

- True S2S (app context): set AppClientId, AppClientSecret, and UseS2SConnection: true. 

- User-delegated S2S: set AppClientId and UseS2SConnection: false. The user signs in interactively.  

##### SDK source 

- .NET: [microsoft/Agents-for-net](https://github.com/microsoft/Agents-for-net/tree/main/src/libraries/Client/Microsoft.Agents.CopilotStudio.Client) (CopilotStudio.Client) 

- JavaScript: [microsoft/Agents-for-js](https://github.com/microsoft/Agents-for-js/tree/main/packages/agents-copilotstudio-client) (agents-copilotstudio-client) 

- Python: [microsoft/Agents-for-python](https://github.com/microsoft/Agents-for-python/tree/main/libraries/microsoft-agents-copilotstudio-client) (microsoft-agents-copilotstudio-client) 

## Scenarios to test 

Whilst the feature is in private preview mode, test these first to validate your Day 0 setup: 

- App-only token + anonymous agent → conversation starts; agent replies. 

- App-only token + authenticated agent → expect S2SDirectEngineRequiresNoAuthentication (correct). 

- OBO token + integrated-auth agent → conversation starts; user identity flows through. 

- Multi-turn with same app identity → succeeds across 2–3 turns. 

- Multi-turn, swap to a different app token mid-conversation → expect CallerIdentityMismatch (objectId validation). 

- Tenant mismatch (token tenant A, agent tenant B) → expect D2EAccessDenied. 

- Sharing read-back → share with App A; close panel; reopen; App A still listed (verify by client ID). 

- Remove an app from sharing → next turn from that D2EAccessDenied. 

- SSE streaming → set Accept: text/event-stream and confirm streamed activities arrive. 

## Reporting Issues

Reach out to your Microsoft Contact with your bug, which should be documented with what you are testing, your setup (do not share passwords/secrets), what is not working, and your environment details. Ensure you have documented replication steps. 

## Security Best Practice Context

Server‑to‑Server (S2S) interactions with Direct‑to‑Engine (D2E) APIs operate without an end‑user context and therefore require agents to use non‑user‑based authentication methods. As a security best practice for enabling S2S D2E access, customers are recommended to enforce Data Loss Prevention (DLP) controls by restricting or blocking all agent channels except those explicitly intended for S2S usage. 

There is a customer content and agreement expected as part of the enablement of this feature under the private preview terms that supports this model while maintaining alignment with enterprise security and governance expectations. Please request enablement through your Microsoft Account Team representatives.
