# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Add optimistic writes to the released SDK Blob provider.

The SDK provider currently overwrites blobs unconditionally. Keep the ETag on
the per-turn StoreItem so concurrent replicas fail rather than lose updates.
"""

import json

from azure.core import MatchConditions
from azure.core.exceptions import ResourceNotFoundError
from microsoft_agents.storage.blob import BlobStorage


class VersionedBlobStorage(BlobStorage):
    async def _read_item(self, key, *, target_cls, **kwargs):
        try:
            download = await self._container_client.download_blob(key, timeout=5)
        except ResourceNotFoundError:
            return None, None
        item = target_cls.from_json_to_store_item(json.loads(await download.readall()))
        item._blob_etag = download.properties.etag
        return key, item

    async def _write_item(self, key, item):
        data = json.dumps(item.store_item_to_json()).encode("utf-8")
        etag = getattr(item, "_blob_etag", None)
        options = {"etag": etag, "match_condition": MatchConditions.IfNotModified} if etag else {}
        result = await self._container_client.get_blob_client(key).upload_blob(
            data=data,
            length=len(data),
            overwrite=bool(etag),
            timeout=5,
            **options,
        )
        item._blob_etag = result["etag"]

    async def close(self):
        await self._close()
