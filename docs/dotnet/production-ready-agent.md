# Prepare a .NET agent for production

Apply the [shared production controls](../shared/production-readiness.md) and
[readiness matrix](../shared/readiness-matrix.md). Use the
[production reference](../../samples/dotnet/production-reference/README.md) for
executable code and the [production skill](../../agent-plugins/agents-for-net/skills/agents-sdk-to-prod/SKILL.md)
for assessment, implementation, verification, and handoff.

## Released SDK baseline

The reference consumes stable Microsoft Agents NuGet packages `1.8.77`.
Install dependencies with the commands in the sample README. No SDK source clone
or custom package build is required. Revalidate API behavior and production
controls when upgrading the released package baseline.

## Hosting and authentication

Register SDK services with `AddAgentDefaults().AddAgent<SupportAgent>()`, but
configure inbound authentication explicitly through ASP.NET Core `AddJwtBearer`
and `AddAuthorization`. SDK adapter registration alone does not secure routes.
Protect `/api/messages` with `RequireAuthorization()` in production. Additional
custom endpoints that mutate state, send messages or call APIs require the same
explicit authorization decision. Health endpoints remain anonymous.

The reference's token-validation parameters require a signed token, signing-key
validation, expiration/lifetime, exact audience and the selected Bot Service
issuer. It uses HTTPS Bot Framework OpenID metadata, disables error details, and
sets `MapInboundClaims=false` so original claim names remain available. Direct
Entra application callers are rejected by this profile's issuer policy.

For Entra/trusted-app scenarios, configure cloud/tenant-specific metadata and
issuers, bind the tenant to verified claims, then apply an allow list to verified
`azp` or `appid`. Keep Bot Service channel authorization distinct from application
and end-user authorization. The reference's legacy Bot Service issuer profile
must be reassessed if the selected channel changes its token family.

The request pipeline applies generic errors, authentication, authorization, a
256 KiB bounded activity read, exact channel/service-URL checks, and then
`IAgentHttpAdapter.ProcessAsync`. Chunked bodies are bounded too. A present verified
`serviceurl` claim must match the activity URL. Keep this ordering when adding middleware.

## Identity and outbound destinations

The reference reads `Connections:ServiceConnection:Settings` from standard
ASP.NET configuration and validates its client/tenant IDs before using `Microsoft.Agents.Authentication.Msal.MsalAuth` and `UserManagedIdentity`.
`ConnectionsMap:0:ServiceUrl='*'` selects the connection; it is not an allow list.
Optional `TokenValidation:Audiences` and `TokenValidation:TenantId` must match
the service connection; omitted values are derived from that connection.

Register `IOutboundHostValidator` explicitly with:

```csharp
new OutboundHostValidator(new OutboundHostValidatorOptions
{
    Enabled = true,
    IncludeDefaultMicrosoftHosts = false,
    Hosts = ["webchat.botframework.com"],
});
```

Entries also match subdomains. The reference's HTTP boundary additionally permits
only the exact HTTPS Web Chat host on port 443, without credentials/query/fragment.
Inspect the main validator's synchronous and async checks before claiming DNS
address protection for a particular call path. Apply separate egress controls to
arbitrary `HttpClient`, model, retrieval and tool calls.

## Durable state, errors and lifecycle

Register `IStorage` with the reference's `VersionedStorage`, backed by a
`BlobContainerClient` and explicit user-assigned `ManagedIdentityCredential`.
The configuration receives a Blob service URL and container name; combine them
for the container URI.
Development uses Azurite. There is no production memory fallback.

The released SDK AgentState writes plain dictionaries without IStoreItem ETags.
`VersionedStorage` carries the Blob version in each turn's state dictionary,
removes that metadata from persisted content, and uses conditional creates and
updates. Preserve its version tokens, surface
conflicts safely, and retry only idempotent domain operations with a bound. The
reference saves state before acknowledging success; it does not replay whole turns.
Verify restart/replica recovery and stale-write rejection against your deployment.

The state has schema `Version=1`. Rollout and rollback require compatible readers
and writers. Configure retention/deletion independently of the application's
conversation lifecycle.

`OnTurnError` sends a generic response and records a bounded failure category.
Keep adapter stack-trace emission disabled. The reference exports only its
application instrumentation source; review SDK attributes/logs before enabling
broader export. Never export tokens, user messages or exception bodies.

ASP.NET hosting drains requests with a bounded shutdown timeout. Telemetry is
flushed at `ApplicationStopped`, after Kestrel drains and before provider disposal.
Keep disposable storage/identity resources owned by the host where supported.

## Verification and operations

Run `dotnet test tests/ProductionReference.Tests.csproj` and
`dotnet format ... --verify-no-changes` with the documented SDK/restore properties.
TestServer checks use real JWT signature/issuer/audience/lifetime validation with
synthetic keys. They do not prove cloud metadata, managed identity, Bot Service
delivery, Azure state recovery, alerts, traffic protection or rollback.

Use the deployment guide and runbook for those separate evidence items. Report
the shared maturity state and outstanding controls instead of a general production claim.
