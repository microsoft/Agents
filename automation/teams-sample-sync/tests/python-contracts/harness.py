# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Run real Agents SDK dispatch with an in-memory outbound activity adapter."""

from copy import deepcopy
import importlib
import importlib.util
from pathlib import Path
from contextlib import contextmanager
import os
import socket
import subprocess
import sys
import time
from weakref import WeakKeyDictionary
from urllib.request import urlopen
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Thread

NETWORK_SPEC = importlib.util.spec_from_file_location(
    "python_network", Path(__file__).resolve().parents[2] / "scripts/python_network.py"
)
NETWORK = importlib.util.module_from_spec(NETWORK_SPEC)
NETWORK_SPEC.loader.exec_module(NETWORK)
NETWORK.restrict_network()

from microsoft_agents.activity import Activity, ResourceResponse
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import TurnContext


APPLICATION_ADAPTERS = WeakKeyDictionary()
PROCESS_SPEC = importlib.util.spec_from_file_location(
    "python_process", Path(__file__).resolve().parents[2] / "scripts/python_process.py"
)
PROCESS = importlib.util.module_from_spec(PROCESS_SPEC)
PROCESS_SPEC.loader.exec_module(PROCESS)


@contextmanager
def file_transfer_server(get_status=200, put_status=201):
    observed = []

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def do_GET(self):
            self.send_response(get_status)
            self.end_headers()
            self.wfile.write(b"notes")

        def do_PUT(self):
            content = self.rfile.read(int(self.headers.get("Content-Length", "0")))
            observed.append({"content": content, "headers": dict(self.headers)})
            self.send_response(put_status)
            self.end_headers()

    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    thread = Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}", observed
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


class MemoryAdapter(CloudAdapter):
    def __init__(self):
        super().__init__(connection_manager=OfflineConnections())
        self.sent = []
        self.continued = []

    async def send_activities(self, context, activities):
        self.sent.extend(
            activity.model_dump(mode="json", by_alias=True, exclude_none=True)
            for activity in activities
        )
        return [ResourceResponse(id=str(index + 1)) for index in range(len(activities))]

    async def update_activity(self, context, activity):
        self.sent.append(activity.model_dump(mode="json", by_alias=True, exclude_none=True))
        return ResourceResponse(id=activity.id)

    async def delete_activity(self, context, reference):
        return None

    async def continue_conversation(self, agent_app_id, continuation_activity, callback):
        self.continued.append({
            "agent_app_id": agent_app_id,
            "activity": continuation_activity.model_dump(mode="json", by_alias=True, exclude_none=True),
        })
        context = TurnContext(self, continuation_activity)
        try:
            await callback(context)
        finally:
            await context._aclose()


BASE_ACTIVITY = {
    "type": "message", "id": "incoming-activity", "channelId": "msteams",
    "serviceUrl": "https://example.invalid",
    "conversation": {"id": "conversation-1", "conversationType": "personal"},
    "from": {"id": "user-1", "name": "Alice", "aadObjectId": "aad-user-1"},
    "recipient": {"id": "agent-1", "name": "Sample Agent"},
}


class OfflineConnections:
    def get_token_provider(self, *args, **kwargs):
        raise AssertionError("Unexpected credential request in an offline contract")


def sample_application(overrides=None):
    module = importlib.import_module("src.agent")
    adapter = MemoryAdapter()
    app = module.create_agent({}, OfflineConnections(), **(overrides or {}))
    APPLICATION_ADAPTERS[app] = adapter
    return app


def invoke_response(sent):
    responses = [activity for activity in sent if activity.get("type") == "invokeResponse"]
    if len(responses) != 1:
        raise AssertionError(f"Expected one invoke response, observed {len(responses)}")
    return responses[0]["value"]


@contextmanager
def sample_http():
    with socket.socket() as reservation:
        reservation.bind(("127.0.0.1", 0))
        port = reservation.getsockname()[1]
    root = sys.argv[1]
    checks = Path(__file__).resolve().parents[2] / "scripts/python_checks.py"
    child = PROCESS.start_process(
        [sys.executable, "-I", "-B", str(checks), "serve", root],
        cwd=root, env={**os.environ, "PORT": str(port)}, stdin=subprocess.DEVNULL,
    )
    url = f"http://127.0.0.1:{port}"
    try:
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if child.poll() is not None:
                raise AssertionError("Sample HTTP process exited before readiness")
            try:
                with urlopen(url, timeout=0.5) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            time.sleep(0.25)
        else:
            raise AssertionError("Sample HTTP readiness timed out")
        yield url
    finally:
        PROCESS.stop_process(child)


async def dispatch(app, payload, teams_client=None):
    adapter = APPLICATION_ADAPTERS.get(app) or MemoryAdapter()
    adapter.sent = []
    activity = Activity.model_validate({**deepcopy(BASE_ACTIVITY), **payload})
    context = TurnContext(adapter, activity)
    if teams_client is not None:
        from microsoft_teams.api import ApiClient
        context.services.set(ApiClient, teams_client)
    try:
        await app.on_turn(context)
        return adapter.sent
    finally:
        await context._aclose()


if __name__ == "__main__":
    import asyncio
    from microsoft_agents.hosting.core import AgentApplication, MemoryStorage
    from microsoft_agents.hosting.msteams import TeamsAgentExtension

    async def smoke():
        app = AgentApplication(storage=MemoryStorage(), connection_manager=OfflineConnections())
        teams = TeamsAgentExtension(app)

        @app.activity("message")
        async def echo(context, state):
            await context.send_activity("routed through SDK")

        sent = await dispatch(app, {"text": "hello"})
        assert sent[0]["text"] == "routed through SDK", sent

        @teams.message_extensions.query("probe")
        async def query(context, state, payload):
            assert payload.command_id == "probe"
            return {"composeExtension": {"type": "message", "text": "SDK invoke passed"}}

        result = await dispatch(app, {
            "type": "invoke", "name": "composeExtension/query",
            "value": {"commandId": "probe", "parameters": []},
        })
        response = invoke_response(result)
        assert response["status"] == 200, response
        assert response["body"]["composeExtension"]["text"] == "SDK invoke passed", response
        print("Real Python SDK Teams dispatch passed")

    asyncio.run(smoke())
