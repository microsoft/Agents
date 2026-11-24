import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sampleHarness } from './harness.mjs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

async function requestConsent (harness) {
  const result = await harness.turn({
    attachments: [{
      contentType: 'application/vnd.microsoft.teams.file.download.info',
      name: 'notes.txt',
      content: { downloadUrl: 'https://example.invalid/download' },
    }],
  })
  const card = result.sent.find((activity) => activity.attachments)?.attachments[0]
  assert.equal(card.contentType, 'application/vnd.microsoft.teams.card.file.consent')
  assert.equal(card.content.sizeInBytes, 5)
  assert.deepEqual(card.content.acceptContext, card.content.declineContext)

  return card.content.acceptContext
}

describe('bot-attachments', () => {
  it('downloaded files request consent, and accepted uploads return file-info cards', async () => {
    const uploads = []
    const harness = await sampleHarness({
      download: async (url) => {
        assert.equal(url, 'https://example.invalid/download')

        return Buffer.from('notes')
      },
      upload: async (url, content) => uploads.push({
        url,
        content: content.toString()
      }),
    })
    const context = await requestConsent(harness)
    const consent = {
      type: 'invoke',
      name: 'fileConsent/invoke',
      value: {
        action: 'accept',
        context,
        uploadInfo: {
          uploadUrl: 'https://example.invalid/upload',
          name: 'notes.txt',
          uniqueId: 'file-1',
          fileType: 'txt',
          contentUrl: 'https://example.invalid/file'
        },
      },
    }
    const accepted = await harness.turn(consent)
    await harness.app.waitForUploads()
    assert.equal(harness.continued.length, 1)
    assert.deepEqual(uploads, [{
      url: 'https://example.invalid/upload',
      content: 'notes'
    }])
    const file = harness.sent.find((activity) => activity.attachments)?.attachments[0]
    assert.equal(file.contentType, 'application/vnd.microsoft.teams.card.file.info')
    assert.equal(file.content.uniqueId, 'file-1')
    assert.equal(accepted.invoke.value.status, 200)
    const replay = await harness.turn(consent)
    assert.equal(uploads.length, 1)
    assert.match(replay.sent[0].text, /pending file is unavailable/)
  })

  it('decline clears the pending upload and ordinary messages return help', async () => {
    let uploads = 0
    const harness = await sampleHarness({
      download: async () => Buffer.from('notes'),
      upload: async () => {
        uploads++
      }
    })
    const context = await requestConsent(harness)
    const declined = await harness.turn({
      type: 'invoke',
      name: 'fileConsent/invoke',
      value: {
        action: 'decline',
        context
      }
    })
    assert.match(declined.sent[0].text, /Declined.*notes.txt/)
    await harness.turn({
      type: 'invoke',
      name: 'fileConsent/invoke',
      value: {
        action: 'accept',
        context,
        uploadInfo: { uploadUrl: 'https://example.invalid/upload' },
      }
    })
    assert.equal(uploads, 0)
    const help = await harness.turn({ text: 'hello' })
    assert.equal(help.sent[0].text, 'Welcome to the Agent Attachments sample!')
  })

  it('failed uploads do not retain reusable pending content', async (context) => {
    context.mock.method(console, 'error', () => {})
    let attempts = 0
    const harness = await sampleHarness({
      download: async () => Buffer.from('notes'),
      upload: async () => {
        attempts++; throw new Error('Transfer failed')
      },
    })
    const consentContext = await requestConsent(harness)
    const consent = {
      type: 'invoke',
      name: 'fileConsent/invoke',
      value: {
        action: 'accept',
        context: consentContext,
        uploadInfo: { uploadUrl: 'https://example.invalid/upload' },
      }
    }
    await harness.turn(consent)
    await harness.turn(consent)
    await harness.app.waitForUploads()
    assert.equal(attempts, 1)
  })

  it('file transfer helpers preserve byte content, upload headers and failure status handling', async (context) => {
    const file = path.resolve(process.argv[2], 'dist/files.js')
    const { downloadAttachment, uploadToOneDrive } = await import(pathToFileURL(file).href)
    context.mock.method(globalThis, 'fetch', async (url, options) => {
      if (options?.method === 'PUT') {
        assert.equal(url, 'https://example.invalid/upload')
        assert.equal(Buffer.from(options.body).toString(), 'notes')
        assert.equal(options.headers['Content-Range'], 'bytes 0-4/5')
        assert.equal(options.headers['Content-Length'], '5')

        return new Response('', { status: 201 })
      }

      return new Response('notes')
    })
    const bytes = await downloadAttachment('https://example.invalid/download')
    await uploadToOneDrive('https://example.invalid/upload', bytes)
    context.mock.method(globalThis, 'fetch', async () => new Response('', { status: 403 }))
    await assert.rejects(downloadAttachment('https://example.invalid/denied'), /Download failed with status 403/)
    await assert.rejects(uploadToOneDrive('https://example.invalid/denied', bytes), /Upload failed with status 403/)
  })

  it('consent acknowledges before a blocked upload finishes, then continues proactively', async () => {
    let release
    const blocked = new Promise((resolve) => {
      release = resolve
    })
    const harness = await sampleHarness({
      download: async () => Buffer.from('notes'),
      upload: async () => blocked,
    })
    const context = await requestConsent(harness)
    let timer
    try {
      const accepted = await Promise.race([
        harness.turn({
          type: 'invoke',
          name: 'fileConsent/invoke',
          value: {
            action: 'accept',
            context,
            uploadInfo: {
              uploadUrl: 'https://example.invalid/upload',
              name: 'notes.txt'
            },
          },
        }),
        new Promise((resolve, reject) => {
          timer = setTimeout(() => reject(new Error('Consent waited for the upload')), 1000)
        }),
      ])
      assert.equal(accepted.invoke.value.status, 200)
      assert.equal(accepted.sent.some((activity) => activity.attachments?.length), false)
      release()
      await harness.app.waitForUploads()
      assert.equal(harness.continued.length, 1)
      assert.ok(harness.sent.some((activity) => activity.attachments?.[0].contentType === 'application/vnd.microsoft.teams.card.file.info'))
    } finally {
      clearTimeout(timer)
      release()
    }
  })
})
