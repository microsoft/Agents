// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
export async function downloadAttachment (url: string): Promise<Buffer> {
  const response = await fetch(url, { signal: AbortSignal.timeout(20_000) })
  if (!response.ok) {
    throw new Error(`Download failed with status ${response.status}`)
  }
  return Buffer.from(await response.arrayBuffer())
}

export async function uploadToOneDrive (url: string, content: Buffer): Promise<void> {
  const response = await fetch(url, {
    method: 'PUT',
    body: new Uint8Array(content),
    signal: AbortSignal.timeout(20_000),
    headers: {
      'Content-Type': 'application/octet-stream',
      'Content-Length': String(content.length),
      'Content-Range': `bytes 0-${content.length - 1}/${content.length}`,
    },
  })
  if (![200, 201].includes(response.status)) {
    throw new Error(`Upload failed with status ${response.status}`)
  }
}
