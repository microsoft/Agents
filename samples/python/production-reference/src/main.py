# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging

from aiohttp import web
from azure.identity.aio import ManagedIdentityCredential
from microsoft_agents.storage.blob import BlobStorageConfig

from .agent import create_agent
from .config import load_config, load_local_environment
from .server import create_server
from .telemetry import Telemetry
from .storage import VersionedBlobStorage


def main():
    load_local_environment()
    config = load_config()
    # SDK diagnostics may include identifiers/content. Export only sample metrics
    # and spans; route failures are recorded as bounded categories.
    logging.getLogger("microsoft_agents").setLevel(logging.CRITICAL)
    credential = (
        ManagedIdentityCredential(client_id=config.client_id) if config.production else None
    )
    storage = VersionedBlobStorage(
        BlobStorageConfig(
            container_name=config.blob_container,
            url=config.blob_url if config.production else "",
            credential=credential,
            connection_string=config.blob_connection_string,
        )
    )
    telemetry = Telemetry(config.telemetry_connection_string if config.production else "")
    agent, auth = create_agent(config, storage, telemetry)

    async def cleanup(app):
        try:
            await storage.close()
            if credential:
                await credential.close()
        finally:
            await telemetry.shutdown()

    web.run_app(
        create_server(config, storage, agent, auth, telemetry, cleanup),
        host="0.0.0.0",
        port=config.port,
        shutdown_timeout=30,
        access_log=None,
    )


if __name__ == "__main__":
    main()
