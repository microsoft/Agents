import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { jsCapabilities } from '../../src/js-capabilities.js'

describe('JS source capabilities', () => {
  it('resolves const helper aliases and rejects unsupported registration forms explicitly', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-js-aliases-'))
    try {
      const file = path.join(root, 'agent.ts')
      const prefix = `
import { AgentApplication } from '@microsoft/agents-hosting'
import { TeamsAgentExtension } from '@microsoft/agents-hosting-extensions-msteams'
const app = new AgentApplication()
const teams = new TeamsAgentExtension(app)
const routes = teams.messageExtensions
const alias = routes
`
      writeFileSync(file, prefix + `
alias.onQuery('search', handler).onQueryLink(handler)
`)
      const facts = jsCapabilities(root)
      assert.equal(facts.composeExtensions, true)
      assert.deepEqual(facts.queryCommands.map((command) => command.id), ['search'])
      assert.ok(facts.linkHandlerEvidence)
      assert.deepEqual(facts.errors, [])
      for (const registration of [
        'const register = routes.onQuery; register("search", handler)',
        'const { onQuery } = routes; onQuery("search", handler)',
        'routes[method]("search", handler)',
        'getRoutes().onQuery("search", handler)',
        'let mutable = routes; mutable.onQuery("search", handler)',
      ]) {
        writeFileSync(file, prefix + registration)
        assert.ok(jsCapabilities(root).errors!.length, registration)
      }
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('recognizes imported aliases and extension registration callback aliases', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-js-routes-'))
    try {
      writeFileSync(path.join(root, 'agent.ts'), `
import { AgentApplication as Application } from '@microsoft/agents-hosting'
import { TeamsAgentExtension as Teams } from '@microsoft/agents-hosting-extensions-msteams'
const app = new Application()
const teams = new Teams(app)
const queryCommand = 'search'
app.registerExtension(teams, (extension) => {
  extension.messageExtensions.onQuery(queryCommand, handler).onQuery('second', handler)
  extension.messageExtensions.onFetchAction('create', handler)
  extension.messageExtensions.onQueryLink(handler)
  extension.fileConsent.onAccept(handler)
  extension.meetings.onStart(handler).onEnd(handler)
  extension.meetings.onParticipantsJoin(handler).onParticipantsLeave(handler)
})
`)
      const facts = jsCapabilities(root)
      assert.equal(facts.bot, true)
      assert.equal(facts.composeExtensions, true)
      assert.deepEqual(facts.queryCommands.map((command) => command.id), ['search', 'second'])
      assert.deepEqual(facts.actionCommands!.map((command) => command.id), ['create'])
      assert.ok(facts.linkHandlerEvidence)
      assert.equal(facts.files, true)
      assert.deepEqual(facts.resourceSpecificPermissions, [
        'OnlineMeeting.ReadBasic.Chat', 'ChannelMeeting.ReadBasic.Group', 'OnlineMeetingParticipant.Read.Chat',
      ])
      assert.deepEqual(facts.errors, [])
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('does not treat comments as registrations and reports dynamic command IDs', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-js-dynamic-'))
    try {
      const file = path.join(root, 'agent.ts')
      writeFileSync(file, '// new AgentApplication(); teams.messageExtensions.onQuery("pretend", handler)')
      assert.equal(jsCapabilities(root).bot, false)
      writeFileSync(file, `
import { AgentApplication } from '@microsoft/agents-hosting'
import { TeamsAgentExtension } from '@microsoft/agents-hosting-extensions-msteams'
const app = new AgentApplication()
const teams = new TeamsAgentExtension(app)
teams.messageExtensions.onQuery(process.env.COMMAND, handler)
`)
      const facts = jsCapabilities(root)
      assert.equal(facts.queryCommands.length, 0)
      assert.match(facts.errors![0]!, /inspectable literal ID/)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})
