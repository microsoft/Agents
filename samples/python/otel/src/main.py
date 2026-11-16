# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from dotenv import load_dotenv

load_dotenv()

from .instrumentation import configure_otel_providers

configure_otel_providers(service_name="OTelAgent")

from .agent import ADAPTER, AGENT_APP, agents_sdk_config
from .start_server import start_server

start_server(
    agent_application=AGENT_APP,
    adapter=ADAPTER,
    config=agents_sdk_config,
)
