# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""HTTP helpers for downloading and uploading attachment bytes."""

import httpx


async def download_attachment(url: str) -> bytes:
    async with httpx.AsyncClient() as client:
        response = await client.get(url)
    if response.status_code != 200:
        raise RuntimeError(f"Download failed with status {response.status_code}")
    return response.content


async def upload_to_onedrive(url: str, content: bytes) -> None:
    file_size = len(content)
    headers = {
        "Content-Type": "application/octet-stream",
        "Content-Length": str(file_size),
        "Content-Range": f"bytes 0-{file_size - 1}/{file_size}",
    }
    async with httpx.AsyncClient() as client:
        response = await client.put(url, content=content, headers=headers)
    if response.status_code not in (200, 201):
        raise RuntimeError(f"Upload failed with status {response.status_code}")
