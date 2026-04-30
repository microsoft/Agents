"""
AgentSettings and related types.

The test framework (or a developer-facing CLI) passes an AgentSettings instance
to the appropriate ConfigWriter before starting the agent process.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional


@dataclass
class Connection:
    """
    Credentials for the agent's service connection.

    Supply either (client_id + client_secret) for a standard app-registration,
    or federated_client_id for a User-Managed Identity / Federated Identity
    Credential setup.
    """

    client_id: str = ""
    client_secret: str = ""
    tenant_id: str = ""
    federated_client_id: str = ""

    def is_federated(self) -> bool:
        return bool(self.federated_client_id)


@dataclass
class AgentSettings:
    """
    SDK-agnostic settings for an agent scenario.

    Designed for a fluent builder style::

        settings = (
            AgentSettings()
            .connection(Connection(client_id="...", client_secret="...", tenant_id="..."))
        )
    """

    _connection: Optional[Connection] = field(default=None, init=False, repr=False)

    def connection(self, conn: Connection) -> "AgentSettings":
        self._connection = conn
        return self

    @property
    def conn(self) -> Optional[Connection]:
        return self._connection
