/** Runs the sample's real AgentApplication with isolated in-memory transport. */
import { createRequire } from 'node:module'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { setTimeout as delay } from 'node:timers/promises'

export async function sampleHarness (dependencies = {}) {
  if (!process.argv[2]) {
    throw new Error('Pass the selected sample root as the first argument')
  }
  const root = path.resolve(process.argv[2])
  const require = createRequire(path.join(root, 'package.json'))
  const { BaseAdapter, TurnContext, INVOKE_RESPONSE_KEY } = require('@microsoft/agents-hosting')
  const { Activity } = require('@microsoft/agents-activity')
  const { createAgent, normalizeIncoming = (activity) => activity } = await import(pathToFileURL(path.join(root, 'dist/agent.js')).href)
  const sent = []
  const continued = []
  class MemoryAdapter extends BaseAdapter {
    async sendActivities (_context, activities) {
      sent.push(...activities.map((activity) => JSON.parse(JSON.stringify(activity))))

      return activities.map((_activity, index) => ({ id: String(index + 1) }))
    }

    async updateActivity (_context, activity) {
      sent.push(JSON.parse(JSON.stringify(activity)))

      return { id: activity.id }
    }

    async deleteActivity () {}

    async continueConversation (identity, reference, logic) {
      continued.push({
        identity,
        reference
      })
      const context = new TurnContext(this, Activity.fromObject({
        ...base,
        ...reference,
        from: reference.user,
        recipient: reference.agent,
        type: 'event',
      }), identity)
      await logic(context)
    }
  }
  const adapter = new MemoryAdapter()
  const app = createAgent(dependencies)
  const base = {
    type: 'message',
    id: 'incoming-activity',
    channelId: 'msteams',
    serviceUrl: 'https://example.invalid',
    conversation: {
      id: 'conversation-1',
      conversationType: 'personal'
    },
    from: {
      id: 'user-1',
      name: 'Alice',
      aadObjectId: 'aad-user-1'
    },
    recipient: {
      id: 'agent-1',
      name: 'Agent'
    },
  }

  return {
    app,
    sent,
    continued,
    async turn (activity) {
      sent.length = 0
      const context = new TurnContext(adapter, Activity.fromObject(normalizeIncoming({
        ...base,
        ...activity
      })), { aud: 'test-agent-app' })
      context.turnState.set(adapter.ConnectorClientKey, {
        httpClient: {
          baseURL: base.serviceUrl,
          defaultHeaders: {}
        },
      })
      await app.run(context)

      return {
        sent: [...sent],
        invoke: context.turnState.get(INVOKE_RESPONSE_KEY),
      }
    },
  }
}

export async function sampleHttp (check) {
  const listener = createServer()
  await new Promise((resolve, reject) => {
    listener.once('error', reject)
    listener.listen(0, '127.0.0.1', resolve)
  })
  const port = listener.address().port
  await new Promise((resolve) => listener.close(resolve))
  const root = path.resolve(process.argv[2])
  const child = spawn(process.execPath, ['dist/index.js'], {
    cwd: root,
    env: {
      PATH: process.env.PATH,
      SystemRoot: process.env.SystemRoot,
      PORT: String(port)
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let output = ''
  let failure
  child.on('error', (error) => {
    failure = error
  })
  child.stdout.on('data', (chunk) => {
    output = (output + chunk).slice(-8000)
  })
  child.stderr.on('data', (chunk) => {
    output = (output + chunk).slice(-8000)
  })
  const url = `http://127.0.0.1:${port}`
  try {
    for (let attempt = 0; attempt < 100; attempt++) {
      if (failure) {
        throw failure
      }
      if (child.exitCode !== null) {
        throw new Error(`HTTP sample exited: ${output}`)
      }
      let ready = false
      try {
        ready = (await fetch(url, { signal: AbortSignal.timeout(100) })).status === 200
      } catch {}
      if (ready) {
        return await check(url)
      }
      await delay(50)
    }
    throw new Error(`HTTP sample did not become ready: ${output}`)
  } finally {
    if (child.exitCode === null) {
      child.kill()
      await new Promise((resolve) => child.once('exit', resolve))
    }
  }
}
