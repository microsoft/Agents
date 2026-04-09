"""
Config writers.

Each writer converts an AgentSettings object into the config file format
expected by a specific SDK language.
"""

from __future__ import annotations

import abc
import json
from pathlib import Path

from .settings import AgentSettings, Connection


class ConfigWriter(abc.ABC):
    @abc.abstractmethod
    def write(self, settings: AgentSettings, path: Path) -> None:
        ...


def _to_env_vars(conn: Connection) -> dict[str, str]:
    prefix = "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__"
    vars: dict[str, str] = {}
    if conn.is_federated():
        vars[prefix + "CLIENTID"] = conn.federated_client_id
        vars[prefix + "TENANTID"] = conn.tenant_id
    else:
        if conn.client_id:
            vars[prefix + "CLIENTID"] = conn.client_id
        if conn.client_secret:
            vars[prefix + "CLIENTSECRET"] = conn.client_secret
        if conn.tenant_id:
            vars[prefix + "TENANTID"] = conn.tenant_id
    return vars


def _to_appsettings(conn: Connection) -> dict:
    s: dict[str, str] = {}
    if conn.is_federated():
        s["ClientId"] = conn.federated_client_id
        s["TenantId"] = conn.tenant_id
    else:
        if conn.client_id:
            s["ClientId"] = conn.client_id
        if conn.client_secret:
            s["ClientSecret"] = conn.client_secret
        if conn.tenant_id:
            s["TenantId"] = conn.tenant_id
    return {"Connections": {"ServiceConnection": {"Settings": s}}}


def _deep_merge(base: dict, override: dict) -> None:
    for key, value in override.items():
        if key in base and isinstance(base[key], dict) and isinstance(value, dict):
            _deep_merge(base[key], value)
        else:
            base[key] = value


class DotEnvWriter(ConfigWriter):
    """Writes a .env file for Python or Node.js agents. Merges with existing vars."""

    def write(self, settings: AgentSettings, path: Path) -> None:
        existing: dict[str, str] = {}
        if path.exists():
            for line in path.read_text(encoding="utf-8").splitlines():
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    k, _, v = line.partition("=")
                    existing[k.strip()] = v.strip()
        if settings.conn:
            existing.update(_to_env_vars(settings.conn))
        path.write_text("\n".join(f"{k}={v}" for k, v in existing.items()) + "\n", encoding="utf-8")


# Node.js uses the same .env format as Python.
NodeEnvWriter = DotEnvWriter


class AppSettingsWriter(ConfigWriter):
    """Writes (or merges into) an appsettings.local.json file for .NET agents."""

    def write(self, settings: AgentSettings, path: Path) -> None:
        doc: dict = {}
        if path.exists():
            try:
                doc = json.loads(path.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                doc = {}
        if settings.conn:
            _deep_merge(doc, _to_appsettings(settings.conn))
        path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
