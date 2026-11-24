# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import importlib
from os import environ
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import jwt
import pytest
from aiohttp import web
from cryptography.hazmat.primitives.asymmetric import rsa
from microsoft_agents.hosting.core.authorization import ClaimsIdentity
from microsoft_agents.hosting.core.storage import MemoryStorage
from microsoft_agents.hosting.core import TurnContext
from microsoft_agents.activity import Activity

from src.agent import create_agent
from src.config import load_config, load_local_environment
from src.issue_capture import advance_issue_capture
from src.server import PAYLOAD_LIMIT, allowed_service_url, create_server
from src.telemetry import Telemetry


CLIENT_ID = "11111111-1111-1111-1111-111111111111"
TENANT_ID = "22222222-2222-2222-2222-222222222222"


def production_env():
    return {
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID": CLIENT_ID,
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID": TENANT_ID,
        "Storage__ServiceUrl": "https://example.blob.core.windows.net",
        "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=33333333-3333-3333-3333-333333333333",
    }


@pytest.mark.parametrize("key", list(production_env()))
def test_production_requires_configuration(key):
    values = production_env()
    del values[key]
    with pytest.raises(ValueError):
        load_config(values)


@pytest.mark.parametrize(
    "key,value",
    [
        ("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET", "secret"),
        ("Storage__ConnectionString", "secret"),
        ("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID", "invalid"),
        ("Storage__ServiceUrl", "http://example.blob.core.windows.net"),
        ("Storage__ServiceUrl", "https://example.blob.core.windows.net?sig=secret"),
        ("APP_ENV", "staging"),
        ("PORT", "0"),
        ("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__AUTHTYPE", "ClientSecret"),
        ("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__VALIDATE_ISSUER", "false"),
        ("CONNECTIONSMAP__0__AUDIENCE", TENANT_ID),
        ("CONNECTIONSMAP__0__CONNECTION", "wrong"),
        ("OutboundHostValidator__Enabled", "false"),
        ("OutboundHostValidator__Hosts", "evil.test"),
    ],
)
def test_unsafe_configuration_rejected(key, value):
    with pytest.raises(ValueError):
        load_config({**production_env(), key: value})


def test_capture_flow_and_invalid_impact():
    state, reply = advance_issue_capture(
        {"version": 1, "stage": "summary"}, "Checkout is unavailable"
    )
    assert state["stage"] == "impact"
    assert "impact" in reply
    assert advance_issue_capture(state, "urgent")[0] == state
    completed, reply = advance_issue_capture(state, " HIGH ")
    assert completed["impact"] == "high"
    assert "saved" in reply
    assert advance_issue_capture(completed, "another issue")[0] == completed
    with pytest.raises(ValueError):
        advance_issue_capture({"version": 2, "stage": "summary"}, "issue")


@pytest.mark.parametrize(
    "url",
    [
        "http://webchat.botframework.com",
        "https://webchat.botframework.com.evil.test",
        "https://user:secret@webchat.botframework.com",
        "http://localhost:3978",
        "https://webchat.botframework.com:444",
        "file:///etc/passwd",
        None,
    ],
)
def test_production_rejects_service_urls(url):
    assert not allowed_service_url(url, True)


def test_local_service_url_policy():
    assert allowed_service_url("https://webchat.botframework.com/", True)
    assert allowed_service_url("http://localhost:3978", False)
    assert not allowed_service_url("https://evil.test", False)


async def make_client(aiohttp_client, storage=None, cleanup=None):
    config = load_config(production_env())
    telemetry = Telemetry()
    adapter = SimpleNamespace(process=AsyncMock(return_value=web.Response(status=202)))
    agent = SimpleNamespace(adapter=adapter)
    _, auth = create_agent(config, storage or MemoryStorage(), telemetry)
    app = create_server(config, storage or MemoryStorage(), agent, auth, telemetry, cleanup)
    return await aiohttp_client(app), adapter, auth


async def test_probes_and_authentication(aiohttp_client):
    client, adapter, auth = await make_client(aiohttp_client)
    assert (await client.get("/health/live")).status == 200
    assert (await client.get("/health/ready")).status == 200
    response = await client.post(
        "/api/messages",
        json={"serviceUrl": "https://webchat.botframework.com/", "channelId": "webchat"},
    )
    assert response.status == 401
    assert await response.json() == {"error": "Request could not be processed."}
    adapter.process.assert_not_awaited()
    assert auth.VALIDATE_ISSUER and not auth.ANONYMOUS_ALLOWED


async def test_dependency_failure_keeps_liveness(aiohttp_client):
    storage = SimpleNamespace(
        write=AsyncMock(side_effect=RuntimeError("secret")), delete=AsyncMock()
    )
    client, _, _ = await make_client(aiohttp_client, storage)
    assert (await client.get("/health/live")).status == 200
    response = await client.get("/health/ready")
    assert response.status == 503
    assert "secret" not in await response.text()


async def test_payload_bound_and_safe_errors(aiohttp_client):
    client, adapter, _ = await make_client(aiohttp_client)
    response = await client.post("/api/messages", data="x" * (PAYLOAD_LIMIT + 1))
    assert response.status == 413
    response = await client.post("/api/messages", data="{invalid")
    assert response.status == 400
    adapter.process.assert_not_awaited()


async def test_authorized_channel_and_claim_checks(aiohttp_client):
    client, adapter, _ = await make_client(aiohttp_client)
    module = importlib.import_module(
        "microsoft_agents.hosting.aiohttp.jwt_authorization_middleware"
    )
    identity = ClaimsIdentity(
        {
            "aud": CLIENT_ID,
            "iss": "https://api.botframework.com",
            "serviceurl": "https://webchat.botframework.com/",
            "exp": 9999999999,
        },
        authentication_type="Bearer",
    )
    with patch.object(module, "_authorize_request", AsyncMock(return_value=identity)):
        activity = {"serviceUrl": "https://evil.test/", "channelId": "webchat"}
        assert (await client.post("/api/messages", json=activity)).status == 400
        activity["serviceUrl"] = "https://webchat.botframework.com/other"
        assert (await client.post("/api/messages", json=activity)).status == 401
        activity["serviceUrl"] = "https://webchat.botframework.com/"
        assert (await client.post("/api/messages", json=activity)).status == 202
        adapter.process.side_effect = RuntimeError("token secret")
        response = await client.post("/api/messages", json=activity)
        assert response.status == 500
        assert "secret" not in await response.text()


async def test_real_jwt_validation_rejects_invalid_claims(aiohttp_client):
    import time

    client, adapter, auth = await make_client(aiohttp_client)
    private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    validator_module = importlib.import_module(
        "microsoft_agents.hosting.core.authorization.jwt.jwt_token_validator"
    )
    signing_key = jwt.PyJWK.from_dict(
        jwt.algorithms.RSAAlgorithm.to_jwk(private_key.public_key(), as_dict=True)
    )
    claims = {
        "aud": CLIENT_ID,
        "iss": "https://api.botframework.com",
        "exp": int(time.time()) + 600,
    }
    # Stub only discovery of the signing key; SDK signature/lifetime/audience/
    # issuer validation remains real.
    with patch.object(validator_module, "PyJWKClient") as jwks:
        jwks.return_value.get_signing_key.return_value = signing_key
        for overrides in (
            {"aud": TENANT_ID},
            {"iss": "https://evil.test"},
            {"iss": "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0"},
            {"exp": 1},
        ):
            token = jwt.encode(
                {**claims, **overrides}, private_key, algorithm="RS256", headers={"kid": "test-key"}
            )
            if overrides.get("iss", "").startswith("https://login.microsoftonline.com/"):
                # Prove the SDK accepts this signed first-party issuer, so the
                # HTTP rejection exercises the reference's stricter boundary.
                verified = await validator_module.JwtTokenValidator(auth).validate_token(token)
                assert verified.claims["iss"] == overrides["iss"]
            response = await client.post(
                "/api/messages",
                json={"serviceUrl": "https://webchat.botframework.com/", "channelId": "webchat"},
                headers={"Authorization": f"Bearer {token}"},
            )
            assert response.status == 401
        adapter.process.assert_not_awaited()
        token = jwt.encode(claims, private_key, algorithm="RS256", headers={"kid": "test-key"})
        response = await client.post(
            "/api/messages",
            json={"serviceUrl": "https://webchat.botframework.com/", "channelId": "webchat"},
            headers={"Authorization": f"Bearer {token}"},
        )
        assert response.status == 202
        adapter.process.assert_awaited_once()


async def test_cleanup_runs_after_server_close(aiohttp_client):
    cleanup = AsyncMock()
    client, _, _ = await make_client(aiohttp_client, cleanup=cleanup)
    await client.close()
    cleanup.assert_awaited_once()


async def test_agent_recovers_state_across_instances():
    storage = MemoryStorage()
    config = load_config(
        {"APP_ENV": "development", "Storage__ConnectionString": "UseDevelopmentStorage=true"}
    )

    async def turn(text):
        agent, _ = create_agent(config, storage, Telemetry())
        context = TurnContext(
            agent.adapter,
            Activity(
                type="message",
                channel_id="webchat",
                service_url="https://webchat.botframework.com/",
                conversation={"id": "recovery-test"},
                from_property={"id": "user"},
                recipient={"id": "agent"},
                text=text,
            ),
        )
        context.send_activity = AsyncMock()
        await agent.on_turn(context)
        return context.send_activity.call_args.args[0]

    assert "impact" in await turn("Checkout is unavailable")
    assert "saved" in await turn("high")
    assert "completed" in await turn("another issue")


@pytest.mark.parametrize("environment", [None, "production", "development"])
def test_dotenv_configures_local_startup_but_not_explicit_production(
    tmp_path, monkeypatch, environment
):
    monkeypatch.delenv("APP_ENV", raising=False)
    # Register an undo before dotenv mutates the real process environment.
    monkeypatch.setenv("Storage__ConnectionString", "")
    monkeypatch.delenv("Storage__ConnectionString", raising=False)
    if environment:
        monkeypatch.setenv("APP_ENV", environment)
    dotenv = tmp_path / ".env"
    dotenv.write_text(
        "APP_ENV=development\nStorage__ConnectionString=UseDevelopmentStorage=true\nPORT=5000\n"
    )
    monkeypatch.setenv("PORT", "3978")
    load_local_environment(dotenv)

    assert environ["PORT"] == "3978"
    if environment in {None, "development"}:
        assert load_config().blob_connection_string == "UseDevelopmentStorage=true"
    else:
        assert "Storage__ConnectionString" not in environ
        assert environ.get("APP_ENV") == environment
