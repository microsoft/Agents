import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sampleHarness } from './harness.mjs'
import { createRequire } from 'node:module'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

describe('bot-meetings', () => {
  it('meeting start sends title, start time and join action', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingStart',
      value: {
        Title: 'Design review',
        StartTime: '2026-10-28T10:00:00Z',
        JoinUrl: 'https://teams.example.com/join'
      },
    })
    const card = result.sent[0].attachments[0].content
    assert.equal(card.body[1].text, '**Title:** Design review')
    assert.match(card.body[2].text, /10:00:00Z/)
    assert.equal(card.actions[0].url, 'https://teams.example.com/join')
  })

  it('meeting end uses Graph resource fallback, retries transcript and parses VTT', async () => {
    const waits = []
    let attempts = 0
    const harness = await sampleHarness({
      meetingInfo: async (_context, id) => {
        assert.equal(id, 'meeting-1')

        return {
          organizer: { aadObjectId: 'organizer-1' },
          details: { msGraphResourceId: 'graph-meeting-1' }
        }
      },
      wait: async (milliseconds) => {
        waits.push(milliseconds)
      },
      transcript: async (_context, resource, user) => {
        assert.equal(resource, 'graph-meeting-1')
        assert.equal(user, 'organizer-1')

        return ++attempts === 2 ? 'WEBVTT\n\n00:00:00.000 --> 00:00:02.000\n<v Alice>Hello team</v>\n' : ''
      },
    })
    const result = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingEnd',
      value: { EndTime: '2026-10-28T11:00:00Z' },
      channelData: { meeting: { id: 'meeting-1' } },
    })
    assert.deepEqual(waits, [30000, 10000])
    assert.equal(attempts, 2)
    const card = result.sent[0].attachments[0].content
    assert.match(card.body[1].text, /11:00:00Z/)
    assert.equal(card.body[3].text, 'Alice: Hello team')
  })

  it('exhausted transcript retries produce a clear fallback', async () => {
    let attempts = 0
    const harness = await sampleHarness({
      meetingInfo: async () => ({ organizer: { aadObjectId: 'user-1' } }),
      wait: async () => {},
      transcript: async () => {
        attempts++

        return ''
      },
    })
    const result = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingEnd',
      value: {},
      channelData: {
        meeting: {
          id: 'meeting-1',
          details: { msGraphResourceId: 'graph-id' }
        }
      },
    })
    assert.equal(attempts, 3)
    assert.match(result.sent[0].attachments[0].content.body[3].text, /Transcript not available/)
  })

  it('participant events retain roles and ignore agent-only events without an AAD ID', async () => {
    const harness = await sampleHarness()
    const event = {
      members: [{
        user: {
          aadObjectId: 'aad-1',
          name: 'Ada'
        },
        meeting: { role: 'Presenter' }
      }]
    }
    const joined = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingParticipantJoin',
      value: event
    })
    assert.equal(joined.sent[0].attachments[0].content.body[0].text, 'Ada has joined the meeting as Presenter.')
    const left = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingParticipantLeave',
      value: event
    })
    assert.equal(left.sent[0].attachments[0].content.body[0].text, 'Ada has left the meeting.')
    const ignored = await harness.turn({
      type: 'event',
      name: 'application/vnd.microsoft.meetingParticipantLeave',
      value: {
        members: [{
          user: { name: 'Agent' },
          meeting: {}
        }],
      }
    })
    assert.deepEqual(ignored.sent, [])
  })

  it('Graph transcript retrieval selects the newest transcript and reads VTT from the beta route', async (context) => {
    const root = path.resolve(process.argv[2])
    const require = createRequire(path.join(root, 'package.json'))
    const { Client } = require('@microsoft/microsoft-graph-client')
    const { getLatestTranscript } = await import(pathToFileURL(path.join(root, 'dist/transcripts.js')).href)
    const requests = []
    context.mock.method(globalThis, 'fetch', async (request) => {
      const url = typeof request === 'string' ? request : request.url
      requests.push(url)
      if (url.endsWith('/content')) {
        return new Response('WEBVTT\n<v Ada>Latest words</v>', { headers: { 'Content-Type': 'text/vtt' } })
      }

      return new Response(JSON.stringify({
        value: [
          {
            id: 'older',
            createdDateTime: '2026-10-28T09:00:00Z'
          },
          {
            id: 'newer',
            createdDateTime: '2026-10-28T10:00:00Z'
          },
        ]
      }), { headers: { 'Content-Type': 'application/json' } })
    })
    const graphContext = {
      getAppGraphClient: (base) => Client.initWithMiddleware({
        baseUrl: base,
        defaultVersion: '',
        authProvider: { getAccessToken: async () => 'contract-token' },
      }),
    }
    const transcript = await getLatestTranscript(graphContext, 'meeting-resource', 'organizer')
    assert.match(transcript, /Latest words/)
    assert.deepEqual(requests, [
      'https://graph.microsoft.com/beta/users/organizer/onlineMeetings/meeting-resource/transcripts',
      'https://graph.microsoft.com/beta/users/organizer/onlineMeetings/meeting-resource/transcripts/newer/content',
    ])
  })
})
