# Runbook — Production Reference Sample

This runbook covers health endpoint interpretation, telemetry queries, common incidents, rollback guidance, and known limits for the production-reference support triage agent.

See [DEPLOYMENT.md](DEPLOYMENT.md) for initial setup and [README.md](README.md) for local development.

## Health endpoints

The agent exposes two health endpoints mapped in `Program.cs`:

| Endpoint | Purpose | Healthy response | Unhealthy response |
|----------|---------|------------------|--------------------|
| `GET /health/live` | Liveness probe — confirms the process is running. No dependency checks. | `200 OK` body: `Healthy` (plain text) | Process crash / host unreachable |
| `GET /health/ready` | Readiness probe — confirms storage configuration is valid. | `200 OK` body: `Healthy` (plain text) | `503 Service Unavailable` body: `Unhealthy` (plain text) |

**Interpretation:**

- `/health/live` returning non-200 means the App Service process has crashed or the platform cannot route to the instance. App Service health check will restart the instance automatically.
- `/health/ready` returning 503 means `StorageConfigurationHealthCheck` detected an invalid or missing storage configuration. The agent will refuse to process messages. Check the `AZURE_BLOB_STORAGE_CONTAINER_URI` app setting and the managed identity role assignment.

**App Service health check configuration:** The Bicep template sets `healthCheckPath: /health/live`. This path is used by App Service to auto-restart unhealthy instances. The readiness endpoint is available for external monitoring or deployment pipelines but is not wired to App Service auto-restart by default.

## Telemetry

### Location

All telemetry is sent to Application Insights, which is backed by a Log Analytics workspace. Both resources are provisioned by the Bicep template.

Navigate to your Application Insights resource in the [Azure Portal](https://portal.azure.com) and open **Logs** to run Kusto queries.

### Example Kusto queries

**Recent requests (last 30 minutes):**

```kusto
requests
| where timestamp > ago(30m)
| project timestamp, name, url, resultCode, duration, success
| order by timestamp desc
| take 50
```

**Failed requests:**

```kusto
requests
| where timestamp > ago(1h) and success == false
| project timestamp, name, url, resultCode, duration, customDimensions
| order by timestamp desc
```

**Dependency failures (outbound calls to Azure Storage or Bot Service):**

```kusto
dependencies
| where timestamp > ago(1h) and success == false
| project timestamp, type, target, name, resultCode, duration, data
| order by timestamp desc
```

**Exceptions:**

```kusto
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, method, assembly, customDimensions
| order by timestamp desc
| take 50
```

**Requests with high latency (over 2 seconds):**

```kusto
requests
| where timestamp > ago(1h) and duration > 2000
| project timestamp, name, url, duration, resultCode
| order by duration desc
```

**Health check probe results:**

```kusto
requests
| where timestamp > ago(1h) and url contains "/health/"
| summarize count(), avg(duration), countif(success == false) by url, bin(timestamp, 5m)
| order by timestamp desc
```

**Custom metrics from the Agents SDK meter:**

```kusto
customMetrics
| where timestamp > ago(1h) and name startswith "Microsoft.Agents"
| project timestamp, name, value, valueCount, valueSum
| order by timestamp desc
```

## Common incidents

### Incident: Storage Blob Data Contributor role missing

**Symptom:** `/health/ready` returns 503. Application logs show `AuthorizationPermissionMismatch` or `403 Forbidden` when the agent attempts to read or write a state blob.

**Cause:** The managed identity does not have the `Storage Blob Data Contributor` role on the blob container, or the role assignment has not propagated yet.

**Remediation:**

1. Confirm the web app has a system-assigned managed identity:
   ```bash
   az webapp identity show --resource-group "<rg-name>" --name "<web-app-name>"
   ```
2. Verify the role assignment:
   ```bash
   az role assignment list \
     --assignee "<principalId>" \
     --scope "<blobContainerResourceId>" \
     --query "[].{role:roleDefinitionName, scope:scope}"
   ```
3. If missing, add the role assignment:
   ```bash
   az role assignment create \
     --assignee "<principalId>" \
     --role "Storage Blob Data Contributor" \
     --scope "<blobContainerResourceId>"
   ```
4. Wait 30–60 seconds for propagation, then restart the web app:
   ```bash
   az webapp restart --resource-group "<rg-name>" --name "<web-app-name>"
   ```

### Incident: Key Vault reference failure

**Symptom:** App Service configuration shows the `ClientSecret` setting value as `Microsoft.KeyVault reference is not resolvable`. Agent startup fails with authentication errors.

**Cause:** The managed identity does not have the `Key Vault Secrets User` role on the Key Vault, or the Key Vault name or secret URI has changed.

**Remediation:**

1. Verify the Key Vault Secrets User role assignment:
   ```bash
   az role assignment list \
     --assignee "<principalId>" \
     --scope "/subscriptions/<sub-id>/resourceGroups/<rg-name>/providers/Microsoft.KeyVault/vaults/<kv-name>" \
     --query "[].{role:roleDefinitionName}"
   ```
2. If missing, add it:
   ```bash
   az role assignment create \
     --assignee "<principalId>" \
     --role "Key Vault Secrets User" \
     --scope "/subscriptions/<sub-id>/resourceGroups/<rg-name>/providers/Microsoft.KeyVault/vaults/<kv-name>"
   ```
3. Confirm the `BotClientSecret` secret exists in the Key Vault:
   ```bash
   az keyvault secret show --vault-name "<kv-name>" --name "BotClientSecret"
   ```
4. In the App Service **Configuration** blade, verify that `Connections__ServiceConnection__Settings__ClientSecret` shows a valid Key Vault reference URI matching the secret version.
5. Restart the web app.

### Incident: Token validation mismatch

**Symptom:** The agent returns `401 Unauthorized` for all incoming messages. Application Insights shows failed requests with `401` result code.

**Cause:** `TokenValidation__Audiences__0` or `TokenValidation__TenantId` does not match the bot registration, or `TokenValidation__Enabled` is `false` while the bot is connecting over a real channel.

**Remediation:**

1. Confirm the Bot Service app ID matches the `botClientId` deployment parameter:
   ```bash
   az bot show --resource-group "<rg-name>" --name "<bot-name>" --query "properties.msaAppId"
   ```
2. Verify App Service settings:
   ```bash
   az webapp config appsettings list --resource-group "<rg-name>" --name "<web-app-name>" \
     --query "[?name=='TokenValidation__Enabled' || name=='TokenValidation__Audiences__0' || name=='TokenValidation__TenantId']"
   ```
3. Correct any mismatch and restart the web app.

### Incident: Readiness probe unhealthy after deploy

**Symptom:** `/health/ready` returns 503 immediately after a new deployment.

**Cause:** `Program.cs` calls `storageOptions.Validate()` at startup. If `AZURE_BLOB_STORAGE_CONTAINER_URI` is not set (for example, if the Bicep deploy did not complete before the app package was deployed), startup validation fails.

**Remediation:**

1. Check that the Bicep deployment completed successfully and the `storageContainerUri` output is present.
2. Verify the `AZURE_BLOB_STORAGE_CONTAINER_URI` app setting is set:
   ```bash
   az webapp config appsettings list --resource-group "<rg-name>" --name "<web-app-name>" \
     --query "[?name=='AZURE_BLOB_STORAGE_CONTAINER_URI']"
   ```
3. If missing, redeploy the Bicep template or set it manually and restart.

## Rollback guidance

### Rollback an application package

App Service maintains a deployment history. To swap back to the previous package:

1. Inspect deployment history without exposing credentials — use one of:
   - **Azure portal:** App Service → Deployment Center → Deployment Logs shows timestamps, statuses, and commit IDs.
   - **Kudu SCM console:** `https://<web-app-name>.scm.azurewebsites.net/` → Deployments (no credentials printed to the console).
   - **Azure CLI (safe):**
     ```bash
     az webapp log deployment list --resource-group "<rg-name>" --name "<web-app-name>"
     ```
2. If using deployment slots, swap back:
   ```bash
   az webapp deployment slot swap \
     --resource-group "<rg-name>" \
     --name "<web-app-name>" \
     --slot staging \
     --target-slot production
   ```
3. Without slots, redeploy the previous `deploy.zip` package via `az webapp deploy`.

### Rollback infrastructure changes

Bicep deployments are incremental by default. To revert a Bicep change, redeploy the previous version of `main.bicep` with the same parameters. Role assignments are idempotent; re-running will not duplicate them.

> **Do not delete and recreate the Key Vault** unless necessary. Soft delete means the vault name is reserved for `softDeleteRetentionInDays`. Purge first if you need to reuse the name.

## Known limits

- **Storage concurrency:** Azure Blob Storage uses optimistic concurrency via ETags. The Agents SDK performs read-modify-write on state blobs. Concurrent requests on the same conversation may result in a concurrency conflict; the SDK retries, but very high concurrency on a single conversation is not a tested scenario.
- **State retention:** State blobs are never automatically deleted. Configure a [Blob Storage lifecycle management policy](https://learn.microsoft.com/azure/storage/blobs/lifecycle-management-overview) to expire old conversation state.
- **Health check depth:** The readiness check validates configuration shape only; it does not perform a live read/write against Blob Storage. A misconfigured managed identity will pass the readiness check but fail at runtime.
- **Bot registration:** The bot registration is not managed by Bicep. If the bot app ID or tenant changes, app settings must be updated manually and the app restarted.
- **Scale-out:** The `B1` plan does not support autoscale. Upgrade the plan SKU and configure autoscale rules before using this sample as a production baseline.
