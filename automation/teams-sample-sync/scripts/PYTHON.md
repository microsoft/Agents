# Trusted Python tooling

These helpers inspect syntax and SDK metadata without importing candidate code.
They are under `scripts/` so the existing validator fingerprint covers their source
and the tool dependency lock. They are wired into the Python profile and validation adapter.

`python_source.py` uses only the standard library. `python_sdk_policy.py` uses
`packaging==26.2`; `python-tools-requirements.txt` permits only verified wheels.
The resolver is pinned to `pip==26.1.1`, and lock conversion rejects reports from
other pip versions. `python_project.py` combines layout, declaration, and AST checks
without executing candidate modules. Supported candidates use `requirements.txt`
with `src/agent.py` and `src/main.py`; project build/install overrides are rejected.
Candidate layout, syntax, and declaration errors return failed-check evidence so
the shared coordinator can perform its bounded repair. Tool execution failures
remain infrastructure errors. Route registration through unresolved helper
parameters is rejected explicitly; keep registrations next to SDK construction
so manifest capability checks can inspect them.

Protected import, contract, and HTTP checks restrict Python socket connections
and DNS lookups to loopback fixtures. Unexpected outbound requests fail even if
candidate code catches the exception. Spawned HTTP hosts install the same guard
before importing the application. This is a deterministic test guard, not an OS
sandbox for hostile native code.
HTTP smoke uses separate hosts for credential-free readiness and configured
anonymous-message rejection. The second host receives synthetic connection values;
`POST /api/messages` must return 401 or 403, with no external identity request.
HTTP 500 and successful anonymous responses both fail. Ambient connection settings
are removed before either host starts so the first check actually lacks credentials.
The SDK policy evaluates markers for Linux x86_64 CPython rather than inheriting
the developer machine's operating system. Callers must supply an exact runtime
patch version and release-specific metadata for all five required SDK packages.

Create a trusted tool environment outside candidate destinations, then install with
an explicit index, wheel-only policy, and hash verification:

```powershell
python -I -m venv .sync/python-tools
.sync/python-tools/Scripts/python.exe -I -m pip install --isolated --index-url https://pypi.org/simple --only-binary=:all: --require-hashes -r automation/teams-sample-sync/scripts/python-tools-requirements.txt
.sync/python-tools/Scripts/python.exe -I automation/teams-sample-sync/scripts/test_python_sdk_policy.py
.sync/python-tools/Scripts/python.exe -I automation/teams-sample-sync/scripts/test_python_source.py
```

On Linux, use `.sync/python-tools/bin/python` for the environment interpreter.
This environment is for trusted tooling only. Candidate dependency installation
must use a separate fresh environment for each validation.

The metadata selector checks SDK-to-SDK constraints, Python requirements, stable
version ordering, yanked releases, markers, and activated extras. It does not prove
that non-SDK dependencies resolve or have compatible wheels. Full wheel resolution,
hashed sample locks, installed-distribution checks, and protected SDK behavior
contracts remain required before a sample can pass validation.
