# Production reference — Web Chat support issue capture

**Runtime:** .NET 10 · **Channel:** Web Chat · **Host:** Azure App Service

This reference captures one issue summary and impact per conversation in durable
state. It does not create or route tickets. The deterministic flow matches the
JavaScript reference using ASP.NET Core and native Agents SDK APIs.

It includes signed JWT/issuer/exact-audience validation, channel/service-URL
checks, opt-in outbound policy, managed-identity Blob state, payload limits,
safe errors, probes, content-free telemetry, graceful hosting shutdown, tests,
Bicep and operating guidance. Source controls do not certify a deployment.

See [the .NET guide](../../../docs/dotnet/production-ready-agent.md),
[shared contract](../../../docs/shared/production-readiness.md), and
[production skill](../../../agent-plugins/agents-for-net/skills/agents-sdk-to-prod/SKILL.md).
Delegated APIs, attachments, transcripts, models, tools, retrieval and proactive
messaging are outside this reference's boundary.

## SDK packages and local setup

Install .NET 10. The sample uses the stable Microsoft.Agents NuGet packages
`1.8.77`, the latest released version at the time this reference was updated.
Restore installs the packages directly from NuGet; no SDK checkout is needed.

```powershell
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet test tests/ProductionReference.Tests.csproj -p:RestoreConfigFile=NuGet.Config
```

To include Blob recovery/concurrency checks, run Azurite and set
`AZURITE_CONNECTION_STRING=UseDevelopmentStorage=true` before running tests.

Run Azurite separately, then:

```powershell
dotnet run --no-build
```

Use Agents Playground at `http://localhost:3978/api/messages`. ASP.NET Core loads
`appsettings.json` and the environment-specific settings. The local launch profile
selects `Development`, so `appsettings.Development.json` enables anonymous local
requests and Azurite. Keep secrets out of committed settings; use .NET user secrets
for local credentials, for example:

```powershell
dotnet user-secrets set "Connections:ServiceConnection:Settings:ClientSecret" "<local-secret>"
```

The project includes a `UserSecretsId`. Azure App Service settings override JSON
values in production.

## Production and verification

`APP_ENV` defaults to `production`. Startup requires identity GUIDs, an HTTPS
Azure Blob service URL and telemetry configuration. Client secrets and storage
connection strings are rejected. The application validates the standard SDK
connection settings and explicitly configures ASP.NET JWT
validation/authorization; adding an adapter alone is insufficient.

The service connection's `AuthType` and `ValidateIssuer`, `ConnectionsMap`, and
`OutboundHostValidator` in `appsettings.json` document the enforced production
policy; startup rejects overrides that weaken or change that boundary.
`ConnectionsMap:0:Audience` and `TokenValidation:Audiences` default to the service
connection client ID; supplied values must match. `TokenValidation:TenantId`
defaults to the service connection tenant ID and must match when supplied.
`OTEL_SERVICE_NAME` sets the exported service identity. All SDK spans are excluded,
and only the sample's reviewed instrumentation is exported.

State uses `VersionedStorage` with the released Azure Blob client and SDK
`IStorage` interface. It carries Blob versions per turn, uses conditional updates
and create-only first writes, and saves before success replies.
The bounded flow changes and saves state once per turn. Repeated changed saves
within one turn require reloading the current Blob version first.
TestServer checks exercise real JWT signature/lifetime/issuer/audience validation
with synthetic keys. The sample exports only its reviewed application source and
bounded counters. Azure recovery, metadata, RBAC, alert, edge, telemetry and
rollback checks require deployed evidence.

See [deployment](DEPLOYMENT.md) and [operations](RUNBOOK.md).
