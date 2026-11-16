# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging

from .agent import ADAPTER, AGENT_APP, agents_sdk_config
from .start_server import start_server

logging.basicConfig(level=logging.INFO)

start_server(
    AGENT_APP,
    adapter=ADAPTER,
    config=agents_sdk_config,
)
