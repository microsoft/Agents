# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# enable logging for Microsoft Agents library
# for more information, see README.md for Quickstart Agent
import logging
ms_agents_logger = logging.getLogger("microsoft_agents")
ms_agents_logger.addHandler(logging.StreamHandler())
ms_agents_logger.setLevel(logging.INFO)

from .agent import ADAPTER, AGENT_APP, agents_sdk_config
from .start_server import start_server

start_server(
    agent_application=AGENT_APP,
    adapter=ADAPTER,
    config=agents_sdk_config,
)
