import pathlib

_AGENTS_DIR_NAME = "agents"
AGENTS_PATH = pathlib.Path.cwd() / _AGENTS_DIR_NAME
ENTRY_POINT_NAME = "_run_agent.ps1"

DEFAULT_LOCAL_AGENT_ENDPOINT = "http://localhost:3978/api/messages"
