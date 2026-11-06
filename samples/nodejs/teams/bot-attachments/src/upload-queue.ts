// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Sample-local work queue. Pending transfers are lost on process restart. */
export class UploadQueue {
  private readonly pending = new Set<Promise<void>>()

  enqueue (work: () => Promise<void>): void {
    // Let the SDK finish the invoke before starting external transfer work.
    const task = new Promise<void>((resolve) => setImmediate(resolve))
      .then(work)
      .catch((error) => {
        console.error('File upload or completion delivery failed:', error)
      })
    this.pending.add(task)
    task.then(() => this.pending.delete(task))
  }

  async drain (): Promise<void> {
    while (this.pending.size > 0) {
      await Promise.all([...this.pending])
    }
  }
}
