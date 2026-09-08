# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging

from .agent import AGENT_APP, CONNECTION_MANAGER
from .start_server import start_server

logging.basicConfig(level=logging.INFO)

start_server(
    AGENT_APP,
    auth_configuration=CONNECTION_MANAGER.get_default_connection_configuration(),
)
