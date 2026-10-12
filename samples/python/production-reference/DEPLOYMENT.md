# Deploy the Python production reference

Install Azure CLI/Bicep and Python 3.12. Use an account allowed to create
App Service, Bot Service/Web Chat, identity, storage, monitoring and RBAC resources.
Select the intended Azure public-cloud subscription/tenant first.

## Build and provision

From this sample directory, install the released packages and run the local
checks in README.md, then build and provision:

```powershell
.venv/Scripts/python -m pytest -q
.venv/Scripts/python -m ruff check src tests scripts
.venv/Scripts/python -m ruff format --check src tests scripts
.venv/Scripts/python scripts/package.py
az bicep build --file infra/main.bicep
az group create --name <resource-group> --location <region>
az deployment group create --resource-group <resource-group> --name <deployment-name> --template-file infra/main.bicep --parameters appName=<unique-name> location=<region>
```

The wrapper uses [the shared infrastructure](../../shared/production-reference/infra/main.bicep).
Name prefixes must produce a valid globally unique storage name (letters/digits,
3–24 characters after removing hyphens and adding `st`). Choose `botSkuName=S1`
when the selected traffic profile requires it.

The template creates a Linux Python 3.12 App Service, user-assigned managed
identity, Azure Bot Web Chat, private-access Blob container, Blob data role,
Application Insights and Log Analytics. It sets `PORT=8000` and startup
`python -m src.main`. Packaging downloads the released SDK wheels and all external
dependencies for Linux Python 3.12, then includes those wheels and a hash-checked
requirements file. Remote build installs with `--no-index --require-hashes` from
that retained wheel set. Packaging fails if a compatible binary wheel is unavailable.

Production settings are derived from infrastructure:

```text
APP_ENV=production
PORT=8000
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID=<user-assigned identity client ID>
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID=<identity tenant ID>
Storage__ServiceUrl=https://<account>.blob.core.windows.net/
Storage__Container=agents-production-reference-state
APPLICATIONINSIGHTS_CONNECTION_STRING=<monitor connection string>
SCM_DO_BUILD_DURING_DEPLOYMENT=true
```

Do not configure `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET` or `Storage__ConnectionString` in production.
Save the artifact hash, SDK package versions and dependency/build evidence in deployment
records. Retain the exact artifact: source requirements allow supported ranges,
while the deployed artifact locks every resolved dependency and its hash.

## Deploy and verify

Use the `webAppName` output from the infrastructure deployment:

```powershell
az webapp deploy --resource-group <resource-group> --name <web-app-name>-app --src-path artifacts/app.zip --type zip
python ../../shared/production-reference/scripts/smoke-test.py https://<web-app-name>.azurewebsites.net
```

Wait for build/startup and identity RBAC propagation, then exercise Test in Web Chat
and restart/replica recovery. A passing smoke test proves probes and missing-JWT
rejection only; follow [the runbook](RUNBOOK.md) for negative security, telemetry,
alert, authenticated traffic-protection and rollback evidence.

The App Service health check uses liveness so a storage outage does not trigger
restart storms. Monitor readiness separately. The sample does not create edge
rate-limit policy, origin-bypass restrictions, alerts, retention/deletion or backups.
Those remain deployment controls to implement and verify before promotion.

## Rollback

Retain the previous compatible `app.zip` and its SDK/dependency hashes. Redeploy
that artifact with `az webapp deploy`, rerun smoke/Web Chat/recovery checks, and
record elapsed time. Do not rebuild an old commit with newly resolved dependencies
and call it the same artifact. State schema compatibility is a rollback prerequisite.
