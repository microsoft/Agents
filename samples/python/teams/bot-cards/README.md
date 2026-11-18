# Agent Cards

This sample demonstrates Adaptive Card actions in Microsoft Teams with the Microsoft 365
Agents SDK. Mention the agent with either command:

| Command | Behavior |
| --- | --- |
| `card actions` | Shows an Adaptive Card with an external link, a required name form, and nested cards. |
| `toggle visibility` | Shows a card with initially hidden content and a visibility toggle. |

Submitting the name form replies with the submitted name. Unknown messages return this help.

## Prerequisites

- Python 3.11.14 on Linux (the platform used to generate `requirements.lock`)
- pip
- An Azure Bot registration and a Microsoft Teams app in a development tenant
- A public HTTPS tunnel for local Teams testing

## Windows PowerShell setup

From this sample directory, install dependencies and prepare `.env`:

```powershell
..\sample.ps1 setup
```

Configure the connection values in `.env`, then start the sample:

```powershell
..\sample.ps1 run
```

No activation or session variables are needed. Setup checks Python 3.11.14,
installs dependencies, checks imports, and preserves an existing `.env`.
Each sample uses `.venv` inside its own directory. Setup reuses an existing
environment and rejects an interpreter-version mismatch.

If Python 3.11.14 is not discovered automatically, select its Windows executable:

```powershell
..\sample.ps1 setup -PythonPath "C:\path\to\python.exe"
```

To keep environments outside the samples, pass `-EnvironmentRoot "C:\venvs"`
to both commands; each sample then gets its own named environment there.
The launcher installs `requirements.txt` on Windows. The checked-in hash lock
is for Linux; reproducible Windows deployment requires a Windows-specific lock.

Windows long-path support is required by the Graph SDK. If setup reports that
it is disabled, run this once in administrator PowerShell, then restart Windows:

```powershell
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -Value 1 -PropertyType DWORD -Force
```

## Configure and run

1. Copy `.env.TEMPLATE` to `.env`.
2. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID`,
   `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET`, and
   `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID` to the Azure Bot app
   registration's application ID, client secret, and tenant ID.
3. Create and activate a fresh virtual environment, then install the validated,
   hash-locked dependencies:

   ```bash
   python -m venv .venv
   source .venv/bin/activate
   python -m pip install --require-hashes -r requirements.lock
   ```

4. Start the agent:

   ```bash
   python -m src.main
   ```

`requirements.lock` was resolved for Python 3.11.14 on Linux; other platforms need a
trusted platform-specific dependency resolution and lock.

The agent listens on `http://localhost:3978`. `GET /` is a credential-free readiness
endpoint; Teams activity requests must use the authenticated `POST /api/messages` route.

To test in Teams, expose port 3978 using a dev tunnel with anonymous tunnel access, then
set the Azure Bot messaging endpoint to `https://<your-tunnel-host>/api/messages`. Register
the app with the same bot ID referenced by the `${{AAD_APP_CLIENT_ID}}` manifest placeholder, package the
manifest with its two icons, and sideload it into the development tenant. Replace sample
metadata and placeholders as appropriate before distributing the app.
