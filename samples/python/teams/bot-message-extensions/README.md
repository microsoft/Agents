# Wikipedia Search Agent

This sample uses the Microsoft 365 Agents SDK to search Wikipedia from the
Teams compose area and preview links to `en.wikipedia.org`. Search results
include Adaptive Cards with thumbnail previews. The agent also responds to
`help` and echoes other messages.

## Requirements

- Python 3.11.14
- An Azure Bot registration and credentials for the selected connection
- A public HTTPS endpoint when connecting the agent to Teams

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

## Install

Create and activate a fresh virtual environment:

```bash
python3.11 -m venv .venv
source .venv/bin/activate
```

The validated dependency lock is for Python 3.11.14 on Linux. Install the
hash-locked dependencies:

```bash
python -m pip install --require-hashes -r requirements.lock
```

For another Python runtime or platform, resolve and validate a platform-specific
lock rather than reusing the Linux wheel hashes.

Copy `.env.TEMPLATE` to `.env` and set
`CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID`,
`CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET`, and
`CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID` for the Azure Bot
connection. Start the agent with:

```bash
python -m src.main
```

The service listens on port 3978 by default. Set `PORT` to use another port.
Configure the Azure Bot messaging endpoint as
`https://<your-public-host>/api/messages`; `GET /` is an unauthenticated
readiness check, while message requests retain JWT authorization.

## Teams app package

The manifest uses the `personal`, `team`, and `groupChat` installation scopes
used by the corresponding .NET and Node.js implementations of this same sample.
Replace `${{AAD_APP_CLIENT_ID}}` with the Teams-managed Azure Bot ID and
`${{APP_NAME_SUFFIX}}` with an optional app-name suffix before packaging.
Package `manifest/manifest.json` with `color.png` and `outline.png` at the
package root. The `wikipediaSearch` query command is titled **Wikipedia Search**
and appears in the compose and command-box surfaces; it is not a slash or
mention command. Link previews are declared for `en.wikipedia.org`.

## Local behavior

The `create_agent` factory accepts an optional async `search_wikipedia(query)`
dependency for offline checks. The default helper queries the Wikipedia API.
Local contracts exercise SDK message-extension routing without requiring
external Wikipedia access or Teams credentials. Tenant authentication, package
upload, compose search, and link unfurling require a configured test tenant.
