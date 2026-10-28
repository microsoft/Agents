# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
from uuid import uuid4

import pytest
from azure.core.exceptions import ResourceExistsError, ResourceModifiedError
from microsoft_agents.hosting.core.state.agent_state import CachedAgentState
from microsoft_agents.storage.blob import BlobStorageConfig

from src.storage import VersionedBlobStorage


@pytest.mark.skipif(
    not os.environ.get("AZURITE_CONNECTION_STRING"),
    reason="Set AZURITE_CONNECTION_STRING for Blob integration checks.",
)
async def test_blob_restart_and_concurrent_writes():
    config = BlobStorageConfig(
        container_name=f"production-reference-test-{uuid4().hex}",
        connection_string=os.environ["AZURITE_CONNECTION_STRING"],
    )
    first = VersionedBlobStorage(config)
    second = VersionedBlobStorage(config)
    key = "conversation.test"
    try:
        await first.write({key: CachedAgentState({"stage": "impact", "version": 1})})
        with pytest.raises(ResourceExistsError):
            await second.write({key: CachedAgentState({"stage": "summary", "version": 1})})
        loaded = (await second.read([key], target_cls=CachedAgentState))[key]
        assert loaded.state["stage"] == "impact"
        stale = (await first.read([key], target_cls=CachedAgentState))[key]
        loaded.state["stage"] = "complete"
        await second.write({key: loaded})
        stale.state["stage"] = "summary"
        with pytest.raises(ResourceModifiedError):
            await first.write({key: stale})
        recovered = (await first.read([key], target_cls=CachedAgentState))[key]
        assert recovered.state["stage"] == "complete"
    finally:
        await first._container_client.delete_container()
        await first.close()
        await second.close()
