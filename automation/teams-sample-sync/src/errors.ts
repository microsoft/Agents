/** Shared error type with no dependency on configuration or target identity. */
export class SyncError extends Error {
  constructor (message: string) {
    super(message)
    this.name = 'SyncError'
  }
}
