const appUrl = process.env.APP_URL?.replace(/\/$/, '')
if (!appUrl) {
  throw new Error('APP_URL is required, for example https://my-agent.azurewebsites.net')
}

const live = await fetch(`${appUrl}/health/live`)
if (!live.ok) throw new Error(`Liveness failed with ${live.status}`)

const ready = await fetch(`${appUrl}/health/ready`)
if (!ready.ok) throw new Error(`Readiness failed with ${ready.status}`)

console.info('Deployment smoke test passed.')
