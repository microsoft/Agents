// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { ResponseType } from '@microsoft/microsoft-graph-client'
import type { TeamsTurnContext } from '@microsoft/agents-hosting-extensions-msteams'

interface Transcript {
  id?: string
  createdDateTime?: string
}

export function parseVtt (vtt: string): string {
  return vtt.split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith('WEBVTT') && !line.includes('-->'))
    .map((line) => {
      let text = line.replace(/<v ([^>]+)>/g, (_tag, speaker: string) => `${speaker.replace(/[<>]/g, '')}: `)
      let previous: string
      do {
        previous = text
        text = text.replace(/<[^>]+>/g, '')
      } while (text !== previous)
      // Incomplete tags and malformed speaker labels must remain plain text.
      return text.replace(/[<>]/g, '').trim()
    })
    .filter(Boolean)
    .join('\n')
}

export async function getLatestTranscript (context: TeamsTurnContext, resourceId: string, userId: string): Promise<string> {
  try {
    const graph = context.getAppGraphClient('https://graph.microsoft.com/beta')
    const route = `/users/${encodeURIComponent(userId)}/onlineMeetings/${encodeURIComponent(resourceId)}/transcripts`
    const response = await graph.api(route).get() as { value?: Transcript[] }
    const latest = [...(response.value ?? [])].sort((left, right) =>
      new Date(right.createdDateTime ?? 0).getTime() - new Date(left.createdDateTime ?? 0).getTime()
    )[0]
    if (!latest?.id) {
      return ''
    }
    return await graph.api(`${route}/${encodeURIComponent(latest.id)}/content`)
      .header('Accept', 'text/vtt').responseType(ResponseType.TEXT).get() as string
  } catch (error) {
    console.error('Error retrieving transcript:', error)
    return ''
  }
}
