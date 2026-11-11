# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from dataclasses import dataclass
from os import environ
from pathlib import Path
from typing import Mapping
from urllib.parse import urlparse
from uuid import UUID

from dotenv import load_dotenv


@dataclass(frozen=True)
class AppConfig:
    production: bool
    port: int
    client_id: str
    tenant_id: str
    blob_url: str
    blob_container: str
    blob_connection_string: str
    client_secret: str
    telemetry_connection_string: str


def load_local_environment(path: Path | None = None) -> None:
    # An explicit deployment environment wins; local .env can select development.
    if environ.get("APP_ENV", "").strip().lower() in {"", "development"}:
        load_dotenv(path or Path(__file__).resolve().parents[1] / ".env", override=False)


def load_config(env: Mapping[str, str] | None = None) -> AppConfig:
    values = environ if env is None else env
    environment = values.get("APP_ENV", "production").strip().lower()
    if environment not in {"production", "development"}:
        raise ValueError("APP_ENV must be production or development.")
    production = environment == "production"

    def value(name: str, default: str = "") -> str:
        return values.get(name, default).strip()

    def required(name: str) -> str:
        result = value(name)
        if not result:
            raise ValueError(f"{name} is required.")
        return result

    port = int(value("PORT", "3978"))
    if not 1 <= port <= 65535:
        raise ValueError("PORT must be between 1 and 65535.")
    client_id = value("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID")
    tenant_id = value("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID")
    blob_url = value("Storage__ServiceUrl")
    connection_string = value("Storage__ConnectionString")
    client_secret = value("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET")
    telemetry = value("APPLICATIONINSIGHTS_CONNECTION_STRING")
    if production:
        expected_policy = {
            "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__AUTHTYPE": "UserManagedIdentity",
            "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__VALIDATE_ISSUER": "true",
            "CONNECTIONSMAP__0__CONNECTION": "SERVICE_CONNECTION",
            "CONNECTIONSMAP__0__SERVICEURL": "*",
            "CONNECTIONSMAP__0__AUDIENCE": client_id,
            "OutboundHostValidator__Enabled": "true",
            "OutboundHostValidator__IncludeDefaultMicrosoftHosts": "false",
            "OutboundHostValidator__Hosts": "webchat.botframework.com",
        }
        for name, expected in expected_policy.items():
            if value(name, expected) != expected:
                raise ValueError(f"{name} must match the production Web Chat policy.")
        UUID(required("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID"))
        UUID(required("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID"))
        parsed = urlparse(required("Storage__ServiceUrl"))
        if (
            parsed.scheme != "https"
            or not parsed.hostname
            or not parsed.hostname.endswith(".blob.core.windows.net")
            or parsed.username
            or parsed.password
            or parsed.query
            or parsed.fragment
            or parsed.path not in {"", "/"}
        ):
            raise ValueError("Storage__ServiceUrl must be an Azure public Blob service HTTPS URL.")
        required("APPLICATIONINSIGHTS_CONNECTION_STRING")
        if connection_string or client_secret:
            raise ValueError(
                "Production requires managed identity, without connection strings or client secrets."
            )
    elif not connection_string:
        raise ValueError("Storage__ConnectionString is required for local Azurite state.")
    return AppConfig(
        production,
        port,
        client_id,
        tenant_id,
        blob_url,
        value("Storage__Container", "agents-production-reference-state"),
        connection_string,
        client_secret,
        telemetry,
    )
