# Task Modules Agent - Python

This sample demonstrates three Microsoft Teams task-module interactions using the
Microsoft 365 Agents SDK: an Adaptive Card form, a hosted custom form, and a
two-step Adaptive Card form. The forms are opened from card buttons, not slash
commands.

## Prerequisites

- Python 3.11.14
- An Azure Bot registration with its client ID, secret, and tenant ID
- The Teams app manifest in `manifest/`
- A public Dev Tunnel when testing Teams dialogs, because Teams must reach the
  custom form URL

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

## Run locally

1. From this directory, create and activate a fresh virtual environment:

   ```bash
   python3.11 -m venv .venv
   source .venv/bin/activate
   ```

2. Install the platform-specific, hash-locked dependencies:

   ```bash
   python -m pip install --require-hashes -r requirements.lock
   ```

   The lock is generated for CPython 3.11.14 on Linux x86_64. Resolve and validate
   a new lock on another platform rather than assuming wheel hashes are portable.

3. Copy `.env.TEMPLATE` to `.env` and set the Azure Bot connection values.
   `BOT_ENDPOINT` must be the public HTTPS Dev Tunnel base URL when testing the
   custom form in Teams. Set `BOT_DOMAIN` to the hostname from `BOT_ENDPOINT`,
   without the scheme or path; this value replaces `${{BOT_DOMAIN}}` in the
   manifest's `validDomains` before packaging.

4. Start the agent:

   ```bash
   python -m src.main
   ```

The host listens on port `3978` by default. `GET /` is a readiness endpoint;
authenticated activity requests are processed at `POST /api/messages`.

## Dialogs

- **Adaptive Card** collects multiline text and returns a submission acknowledgement.
- **Custom Form** serves `/customform` and collects a name and email address.
- **Multi-step Form** collects a name, carries it into the email step, then sends
  a personalized completion message.

Provision the Azure Bot messaging endpoint as
`https://<your-devtunnel-domain>/api/messages`. The sample manifest uses local
sample identity placeholders; replace them and the dialog domain with the
values for your app before packaging or distribution.

The custom HTML dialog uses TeamsJS in the browser to initialize the Teams
client context and submit dialog results. The Python Agents SDK handles the
server-side activities; it does not replace this browser client library.
