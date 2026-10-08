# Production reference — Web Chat support issue capture

> **Tier:** 3 production reference · **Runtime:** JavaScript/TypeScript · **Channel:** Web Chat · **Host:** Azure App Service

This sample is a deployable reference, not a production certification. It captures one issue summary and impact per conversation in durable state. It does not classify, route, or create a support ticket. The deterministic two-message flow keeps the production hosting controls clear and reproducible.

## Coverage

- Web Chat JWT authentication, issuer and exact-audience validation, and service URL validation.
- A bounded outbound-host policy for token-bearing Web Chat replies.
- Azure Blob durable state: Azurite locally; managed identity in Azure.
- 256 KB JSON limit, SDK authentication errors, generic application errors, liveness, write-capable storage readiness, and graceful shutdown.
- Azure Monitor OpenTelemetry export for built-in Agents SDK instrumentation and small application-specific spans and metrics.
- App Service, Azure Bot, and Web Chat Bicep; deployment instructions; and operator runbook.

Tools, proactive messages, attachment handling, transcripts, and other channels are intentionally excluded. See [the production guide](../../../docs/production-ready-agent-js.md) before adding them.

Use the guide to plan or review production requirements. Use this sample to inspect, run, or deploy the stated reference path. Use the AI skill when you want an AI coding agent to assess or harden a selected scenario and add its applicable feature-specific controls.

## Hosting choice

Use the SDK `startServer` helper for a simple agent that accepts its default JSON parser, endpoint pipeline, and shutdown behavior. This sample owns its Express host because it must set the JSON limit before the agent route, apply JWT authentication before its Web Chat host policy, add error handling after the route, and close the HTTP server before telemetry shutdown. The health routes remain anonymous. The SDK helper's optional rate limit is IP-based and runs before JWT validation; it is not a trusted per-user limit for Web Chat.

The start commands preload `telemetry.js` so Azure Monitor initializes before Agents SDK components run. The SDK then emits its built-in instrumentation automatically. Set `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` only when a built-in category is not required; valid values are `STORAGE`, `AUTHENTICATION`, `AUTHORIZATION`, and `DIALOGS`.

## Local run

1. Install Node 24 LTS and the sample dependencies.

   ```bash
   cd samples/nodejs/production-reference
   npm ci
   npm run build
   ```

2. Start Azurite in a separate terminal:

   ```bash
   npx azurite --silent
   ```

3. Copy `env.TEMPLATE` to `.env`. Keep `NODE_ENV=development`.

4. Start the sample:

   ```bash
   npm run start:local
   ```

5. Check probes:

   ```bash
   curl http://localhost:3978/health/live
   curl http://localhost:3978/health/ready
   ```

6. Use Agents Playground or a configured Web Chat client. Local anonymous testing is for development only. Production requires `NODE_ENV=production` and complete Web Chat connection configuration.

## Verify

```bash
npm test
az bicep build --file infra/main.bicep
```

`npm test` covers the deterministic issue-capture flow, production configuration, probes, payload limit, and HTTP authentication boundary. Add real Web Chat deployment tests before promoting an environment. See [DEPLOYMENT.md](DEPLOYMENT.md) and [RUNBOOK.md](RUNBOOK.md).

## Configuration rules

- Production requires `BLOB_CONTAINER_URL`, Application Insights connection string, `clientId`, `tenantId`, `authType`, issuer validation, the complete connection map, and the bounded outbound-host policy.
- Bicep provisions one user-assigned managed identity, attaches it to App Service, grants it Blob access, and configures Azure Bot to use it. Its client ID is the exact connection-map audience. Bicep also creates Web Chat.
- The SDK requires `connectionsMap` service URL `*` to select its default named connection. In production, JWT audience and issuer validation run first. HTTP middleware then rejects a service URL outside `webchat.botframework.com`. The configured outbound-host policy restricts token-bearing SDK client paths. Development allows the loopback service URL used by Agents Playground.
- This sample deliberately sets `OutboundHostValidator__IncludeDefaultMicrosoftHosts=false` because its production boundary contains only Web Chat. It explicitly lists `webchat.botframework.com`. Add and test every required host before extending the sample to another channel or token-bearing SDK client.
- `BLOB_CONTAINER_URL` includes the container path, for example `https://contoso.blob.core.windows.net/agents-production-reference-state`.
- Bicep grants the agent identity `Storage Blob Data Contributor` on the state storage account.
- This sample uses no user-delegated downstream API. It shows inbound channel authentication only. Add user authorization when a feature calls an API for the signed-in user.
- This Web Chat-only path receives calls through Azure Bot Service. It does not use an application caller allow list. Add an `azp`/`appid` allow list after JWT validation for agent-to-agent or trusted-application endpoints.

## Intentional limits

- Apply rate limits at an authenticated edge such as Front Door or API Management. Do not limit by the Azure Bot connector IP inside the app. The supplied Bicep does not provision this organization-specific edge.
- AI, retrieval, and tools are conditional controls. Add them only with the guide's input/output, authorization, evaluation, and side-effect controls.
- Blob ETag conflicts are surfaced by the provider. A high-contention workflow must retry at the domain layer with a bounded policy.
