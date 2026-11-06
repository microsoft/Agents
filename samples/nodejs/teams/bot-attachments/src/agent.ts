// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { randomUUID } from 'node:crypto'
import { Activity } from '@microsoft/agents-activity'
import { AgentApplication, Conversation, MemoryStorage } from '@microsoft/agents-hosting'
import { TeamsAgentExtension } from '@microsoft/agents-hosting-extensions-msteams'
import { downloadAttachment, uploadToOneDrive } from './files.js'
import { UploadQueue } from './upload-queue.js'

interface FileContext {
  filename?: string
  file_id?: string
}

export function createAgent (services: {
  download?: typeof downloadAttachment
  upload?: typeof uploadToOneDrive
} = {}) {
  const download = services.download ?? downloadAttachment
  const upload = services.upload ?? uploadToOneDrive
  const queue = new UploadQueue()
  const pendingUploads = new Map<string, Buffer>()
  const app = new AgentApplication({ storage: new MemoryStorage() })
  const teams = new TeamsAgentExtension(app)
  app.registerExtension(teams, (extension) => {
    extension.fileConsent.onAccept(async (context, _state, consent) => {
      const details = (consent.context ?? {}) as FileContext
      const id = details.file_id ?? ''
      const content = pendingUploads.get(id)
      pendingUploads.delete(id)
      if (!content || !consent.uploadInfo?.uploadUrl) {
        await context.sendActivity('The pending file is unavailable. Send the file again to request consent.')
        return
      }
      await context.sendActivity(`Accepted. Uploading <b>${details.filename}</b>...`)
      const conversation = new Conversation(context)
      conversation.reference = structuredClone(conversation.reference)
      const adapter = context.adapter
      const info = structuredClone(consent.uploadInfo)
      const uploadUrl = consent.uploadInfo.uploadUrl
      queue.enqueue(async () => {
        await upload(uploadUrl, content)
        await app.proactive.continueConversation(adapter, conversation, async (continued) => {
          await continued.sendActivity(Activity.fromObject({
            type: 'message',
            text: `<b>${info.name}</b> has been successfully uploaded.`,
            attachments: [{
              contentType: 'application/vnd.microsoft.teams.card.file.info',
              name: info.name,
              contentUrl: info.contentUrl,
              content: { uniqueId: info.uniqueId, fileType: info.fileType },
            }],
          }))
        })
      })
    })
    extension.fileConsent.onDecline(async (context, _state, consent) => {
      const details = (consent.context ?? {}) as FileContext
      pendingUploads.delete(details.file_id ?? '')
      await context.sendActivity(`Declined. We won't upload file <b>${details.filename}</b>.`)
    })
  })
  app.onActivity('message', async (context) => {
    const attachment = context.activity.attachments?.[0]
    if (attachment?.contentType !== 'application/vnd.microsoft.teams.file.download.info' || !attachment.name) {
      await context.sendActivity('Welcome to the Agent Attachments sample!')
      return
    }
    const info = attachment.content as { downloadUrl?: string }
    if (!info.downloadUrl) {
      throw new Error('Teams file attachment did not include a download URL')
    }
    const content = await download(info.downloadUrl)
    const id = randomUUID()
    pendingUploads.set(id, content)
    const consentContext = { filename: attachment.name, file_id: id }
    await context.sendActivity(`Received <b>${attachment.name}</b>. Requesting permission to save to your OneDrive...`)
    await context.sendActivity(Activity.fromObject({
      type: 'message',
      attachments: [{
        contentType: 'application/vnd.microsoft.teams.card.file.consent',
        name: attachment.name,
        content: {
          description: 'This is the file I want to send you',
          sizeInBytes: content.length,
          acceptContext: consentContext,
          declineContext: consentContext,
        },
      }],
    }))
  })
  return Object.assign(app, { waitForUploads: () => queue.drain() })
}
