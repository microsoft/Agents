# Agent Meetings Sample (Python)

This sample uses the Microsoft 365 Agents SDK to handle Teams meeting start,
end, participant join, and participant leave events. On meeting end, it looks
up the newest available VTT transcript through Microsoft Graph and displays
speaker lines in a card. It does not expose slash or mention commands.

## Prerequisites

- Python 3.11
- A Microsoft 365 tenant with Teams and permission to install custom apps
- A Teams-managed bot registration and a public HTTPS tunnel
- A tenant administrator who can grant Microsoft Graph application permissions
  and configure an online meeting application access policy

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

## Set up the sample

From this directory, create and activate a virtual environment, then install
the hash-locked dependencies:

```bash
python3.11 -m venv .venv
source .venv/bin/activate
python -m pip install --require-hashes -r requirements.lock
```

The lock is resolved for Python 3.11.14 on the platform recorded by the
synchronization validation. Resolve a new lock for a different Python runtime
or platform rather than reusing platform-specific wheel hashes.

Copy `env.TEMPLATE` to `.env`. Set the service-connection client ID, secret,
and tenant ID for inbound agent authentication. Set `TENANT_ID`, `CLIENT_ID`,
and `CLIENT_SECRET` to the app registration used for app-only Graph access.
The Graph credentials can use that same registration. Keep `.env` private and
never commit secrets.

Start a public tunnel for port 3978, for example:

```bash
devtunnel host -p 3978 --allow-anonymous
```

Create a Teams-managed app with its messaging endpoint set to
`https://<your-tunnel-domain>/api/messages`, then set the app ID, bot ID, and
tunnel host in `manifest/manifest.json`. Install that manifest package in
Teams and add the agent to a meeting. `GET /` is a public readiness check;
`POST /api/messages` remains protected by SDK JWT authorization.

## Configure meeting events and Graph

Teams meeting event delivery requires a Teams-managed bot, not an Azure Bot
resource. In the Teams Developer Portal, enable the **Participant Join** and
**Participant Leave** subscriptions.

For app-only transcript lookup, add the following Microsoft Graph **application
permissions** to the Entra app registration and grant tenant-wide admin consent:

- `OnlineMeetings.Read.All`
- `OnlineMeetingTranscript.Read.All`

Configure an online meeting application access policy for each organizer whose
meetings the agent should access:

- [Configure an application access policy](https://learn.microsoft.com/graph/cloud-communication-online-meeting-application-access-policy)
- [Manage Teams policies with PowerShell](https://learn.microsoft.com/microsoftteams/teams-powershell-managing-teams#manage-policies-via-powershell)

The manifest declares meeting resource-specific consent for event delivery.
Those manifest permissions do not replace the Microsoft Graph application
permissions, admin consent, or organizer application-access policy above.
No tenant permissions or policies are configured by running this sample.

## Transcript behavior

When a meeting ends, the agent prefers the `msGraphResourceId` supplied by
Teams. If it is absent, and the meeting metadata contains both an organizer and
a join URL, the agent looks up the online meeting for that organizer only.
It waits 30 seconds before the first Graph lookup or transcript request, then
retries up to two more times with 10-second waits. Once a Graph ID is found, it
is reused for transcript retries. The newest transcript is requested as VTT and
formatted into speaker lines. Missing metadata, unavailable transcripts, or
Graph failures produce a clear unavailable response.

To run the agent after installing dependencies:

```bash
python -m src.main
```

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [Teams meeting event APIs](https://learn.microsoft.com/en-us/microsoftteams/platform/apps-in-teams-meetings/meeting-apps-apis)
- [Microsoft Graph online meeting lookup](https://learn.microsoft.com/en-us/graph/api/onlinemeeting-get?view=graph-rest-1.0)
- [Microsoft Graph meeting transcripts](https://learn.microsoft.com/graph/api/resources/calltranscript)
