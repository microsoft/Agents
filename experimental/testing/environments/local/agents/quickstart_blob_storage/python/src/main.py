# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from .agent import AGENT_APP, CONNECTION_MANAGER
from .start_server import start_server

start_server(
    agent_application=AGENT_APP,
    auth_configuration=CONNECTION_MANAGER.get_default_connection_configuration(),
)
