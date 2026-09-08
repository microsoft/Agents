# Microsoft Entra Agent ID sidecar (.NET)

This .NET 8 sample hosts a simple Microsoft 365 Agents SDK agent on port
`3978`. The Agents SDK obtains outbound channel tokens through the Microsoft
Entra Agent ID sidecar, so the agent process does not load a Blueprint
credential or use MSAL.

The sample intentionally does not call Microsoft Graph. It requires no Graph
application permission or tenant-wide application-permission grant.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker Desktop or another Docker Compose implementation
- [Dev Tunnels CLI](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started)
- An Agent Identity Blueprint application
- Its BlueprintPrincipal service principal, created explicitly
- A child Agent Identity service principal
- A local-development client secret created on the Blueprint

Credentials belong to the Blueprint. A child Agent Identity cannot own a
client secret, certificate, or federated identity credential.

## Configure

Copy the environment template and set the Blueprint values:

```powershell
Copy-Item env.TEMPLATE .env
```

- `BLUEPRINT_TENANT_ID` is the tenant containing the Blueprint.
- `BLUEPRINT_CLIENT_ID` is the Blueprint application (client) ID.
- `BLUEPRINT_CLIENT_SECRET` is a local-development Blueprint secret value.
- `AGENT_IDENTITY_CLIENT_ID` identifies the child Agent Identity used by the
  test activity. It is not a credential and is not sent to the sidecar through
  Compose.

The sidecar declares two downstream APIs:

- `agenticblueprint` supplies the Blueprint token used for the Agent Identity
  `fmi_path` exchange.
- `botframework` supplies outbound Agents SDK callback tokens.

The Agents SDK supplies the child Agent Identity and token mode per request.
The sidecar therefore permits overrides only on
`GetAuthorizationHeaderUnauthenticated`. Do not expose the sidecar to other
workloads.

## Run

```powershell
docker compose up --build
```

The agent and sidecar share a network namespace. The agent reaches the sidecar
at `http://localhost:5000`, while only `127.0.0.1:3978` is published to the
host. Sidecar port `5000` is not published.

To run the agent outside Compose, start a correctly configured sidecar on
localhost and run:

```powershell
$env:SIDECAR_URL = "http://localhost:5000"
$env:TokenValidation__Audiences__0 = "<blueprint-client-id>"
$env:TokenValidation__TenantId = "<tenant-id>"
dotnet run
```

## Test inbound authentication locally

Agents Playground can create mock activities, but it cannot mint an Agent
Identity instance token. Because this sample rejects anonymous requests,
Playground requests receive `401 Unauthorized`. Use a Microsoft 365 surface
such as Teams to exercise the complete inbound authentication flow.

1. Start the sample with `docker compose up --build`.
2. Sign in to Dev Tunnels, create a persistent anonymous tunnel, expose port
   `3978`, and host the tunnel:

   ```powershell
   devtunnel user login
   devtunnel create --allow-anonymous
   devtunnel port create <tunnel-id> -p 3978
   devtunnel host <tunnel-id>
   ```

   `--allow-anonymous` allows Microsoft 365 to reach the tunnel. It does not
   disable the agent's JWT validation.
3. Set the Agent Identity Blueprint's messaging endpoint to the HTTPS tunnel
   URL followed by `/api/messages`, for example
   `https://<tunnel-host>:3978/api/messages`. In Teams Developer Portal, open
   the Blueprint configuration, select **API Based**, set **Notification URL**,
   and save it.
4. Follow [Configure a pro-code Agent Identity instance for
   Teams](https://learn.microsoft.com/microsoft-agent-365/developer/create-instance)
   to configure the Blueprint, obtain tenant approval, and open the resulting
   agent instance in Teams. Copilot Studio provisions Agent IDs automatically;
   see [Agent identity integration for Copilot
   Studio](https://learn.microsoft.com/microsoft-agent-365/builder/identity).
   Keep the tunnel and the local sample running.
5. Send:

   ```text
   caller
   ```

Microsoft 365 sends a signed bearer token to the tunnel endpoint. The token's
audience must be the Blueprint application client ID and its issuer tenant must
match `BLUEPRINT_TENANT_ID`. The activity identifies the child Agent Identity
instance separately through `recipient.agenticAppId`.

The `caller` command displays an Adaptive Card with selected inbound JWT claims
(`iss`, `aud`, `tid`, `sub`, `azp`/`appid`, `ver`, `iat`, and `exp`) plus the
activity recipient role, Agent Identity, Agent User, and tenant. The raw token
is never displayed.

Every other message is echoed. Send `help` to display usage.

## Authentication boundaries

- **Inbound channel validation:** enabled and restricted to the Blueprint
  application client ID and tenant. Missing, expired, incorrectly signed, or
  wrong-audience tokens are rejected before agent code runs.
- **Outbound channel authentication:** `ServiceConnection` uses
  `Microsoft.Agents.Authentication.EntraAuthSidecar.SidecarAuth`. No channel
  client secret is configured in the agent.
- **Blueprint credential:** exists only in the sidecar container.

## Production

Replace the local client secret with workload identity federation or another
supported credential source. Keep the sidecar in the same trusted pod or
network boundary, never publish its port, and enable inbound channel token
validation.

## Troubleshooting

- **`ClientSecret is required`:** rebuild the sample and confirm
  `ServiceConnection` uses `SidecarAuth`, not an MSAL provider.
- **BlueprintPrincipal missing:** create the mandatory BlueprintPrincipal
  before creating child Agent Identities.
- **AADSTS7000215 names the child Agent Identity:** put the credential on the
  Blueprint, not the child identity, and verify the child belongs to that
  Blueprint.
- **AgenticUser callback fails:** do not statically configure
  `DownstreamApis__botframework__RequestAppToken`; the provider selects the
  token mode per request.
- **Sidecar cannot be reached:** use `http://localhost:5000` with the provided
  shared-network-namespace Compose configuration.
- **Channel callback token failure:** confirm the `botframework` scope is
  `5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default`.
- **Inbound 401:** confirm the caller sends a bearer token for the Blueprint
  audience and that `BLUEPRINT_TENANT_ID` matches the token issuer.
- **Agents Playground returns 401:** expected. Playground does not mint an
  Agent Identity instance token; test through Teams or another authenticated
  Microsoft 365 surface.

## Related documentation

- [Test Agent 365 agents with Dev Tunnels](https://learn.microsoft.com/microsoft-agent-365/developer/test-with-devtunnels)
- [Configure an agent messaging endpoint](https://learn.microsoft.com/microsoft-agent-365/developer/agent-messaging-endpoint)
- [Configure a pro-code Agent Identity instance for Teams](https://learn.microsoft.com/microsoft-agent-365/developer/create-instance)
- [Agent identity integration for Copilot Studio](https://learn.microsoft.com/microsoft-agent-365/builder/identity)
- [Agent ID setup instructions](https://learn.microsoft.com/entra/agent-id/identity-platform/agent-id-setup-instructions)
- [Microsoft Entra SDK for AgentID sidecar](https://learn.microsoft.com/entra/msidweb/agent-id-sdk/overview)
