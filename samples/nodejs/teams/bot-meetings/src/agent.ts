// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { setTimeout as delay } from 'node:timers/promises'
import { Activity } from '@microsoft/agents-activity'
import { AgentApplication, MemoryStorage } from '@microsoft/agents-hosting'
import { TeamsAgentExtension, type TeamsTurnContext } from '@microsoft/agents-hosting-extensions-msteams'
import { getLatestTranscript, parseVtt } from './transcripts.js'

interface MeetingInfo {
  organizer?: { aadObjectId?: string }
  details?: { msGraphResourceId?: string }
}

interface MeetingEvent {
  Title?: string
  StartTime?: string
  EndTime?: string
  JoinUrl?: string
}

function messageCard (body: object[], actions: object[] = []) {
  return Activity.fromObject({
    type: 'message',
    attachments: [{
      contentType: 'application/vnd.microsoft.card.adaptive',
      content: { type: 'AdaptiveCard', version: '1.5', body, actions },
    }],
  })
}

export function createAgent (services: {
  meetingInfo?: (context: TeamsTurnContext, id: string) => Promise<MeetingInfo>
  transcript?: typeof getLatestTranscript
  wait?: (milliseconds: number) => Promise<unknown>
} = {}) {
  const meetingInfo = services.meetingInfo ?? (async (context, id) => context.client.meetings.getById(id))
  const transcript = services.transcript ?? getLatestTranscript
  const wait = services.wait ?? delay
  const app = new AgentApplication({ storage: new MemoryStorage() })
  const teams = new TeamsAgentExtension(app)
  app.registerExtension(teams, (extension) => {
    extension.meetings.onStart(async (context, _state, details) => {
      const event = (context.activity.value ?? {}) as MeetingEvent
      await context.sendActivity(messageCard([
        { type: 'TextBlock', text: 'The meeting has started.', weight: 'Bolder', size: 'Large', wrap: true },
        { type: 'TextBlock', text: `**Title:** ${event.Title ?? details.title ?? 'N/A'}`, wrap: true },
        { type: 'TextBlock', text: `**Start Time:** ${event.StartTime ?? details.scheduledStartTime ?? 'N/A'}`, wrap: true },
      ], [{ type: 'Action.OpenUrl', title: 'Join Meeting', url: event.JoinUrl ?? details.joinUrl }]))
    })
    extension.meetings.onEnd(async (context, _state, details) => {
      const meeting = context.activity.channelData?.meeting as { id?: string; details?: { msGraphResourceId?: string } } | undefined
      if (!meeting?.id) {
        console.error('meetingEnd event received without channelData.meeting.id')
        return
      }
      const info = await meetingInfo(context, meeting.id)
      const resourceId = meeting.details?.msGraphResourceId ?? info.details?.msGraphResourceId
      const userId = info.organizer?.aadObjectId ?? ''
      let text = ''
      await wait(30_000)
      if (resourceId) {
        for (let attempt = 0; attempt < 3; attempt++) {
          const vtt = await transcript(context, resourceId, userId)
          if (vtt) {
            text = parseVtt(vtt)
            break
          }
          if (attempt < 2) {
            await wait(10_000)
          }
        }
      }
      const event = (context.activity.value ?? {}) as MeetingEvent
      const lines = text ? text.split('\n') : ['Transcript not available for this meeting.']
      await context.sendActivity(messageCard([
        { type: 'TextBlock', text: 'The meeting has ended.', weight: 'Bolder', size: 'Large', wrap: true },
        { type: 'TextBlock', text: `**End Time:** ${event.EndTime ?? details.scheduledEndTime ?? ''}`, wrap: true },
        { type: 'TextBlock', text: '**Transcript:**', weight: 'Bolder', wrap: true },
        ...lines.map((line) => ({ type: 'TextBlock', text: line, wrap: true })),
      ]))
    })
    extension.meetings.onParticipantsJoin(async (context, _state, details) => {
      const participant = details.members?.[0]
      if (!participant?.user?.aadObjectId) {
        return
      }
      await context.sendActivity(messageCard([{
        type: 'TextBlock',
        text: `${participant.user.name || 'A participant'} has joined the meeting as ${participant.meeting?.role || 'a participant'}.`,
        weight: 'Bolder',
        wrap: true,
      }]))
    })
    extension.meetings.onParticipantsLeave(async (context, _state, details) => {
      const participant = details.members?.[0]
      if (!participant?.user?.aadObjectId) {
        return
      }
      await context.sendActivity(messageCard([{
        type: 'TextBlock',
        text: `${participant.user.name || 'A participant'} has left the meeting.`,
        weight: 'Bolder',
        wrap: true,
      }]))
    })
  })
  return app
}
