const root = process.env.API_URL ?? 'http://localhost:5080/api'
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
async function call(path, token, method = 'GET', body) {
  const response = await fetch(`${root}${path}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const data = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${JSON.stringify(data)}`)
  return data
}
async function waitForPump(token, pumpId, running) {
  for (let attempt = 0; attempt < 30; attempt++) {
    const tanks = await call('/water-supply/status', token)
    const pump = tanks.flatMap(tank => tank.pumps).find(item => item.id === pumpId)
    if (pump?.isRunning === running && !String(pump.status).includes('Esperando')) return pump
    await new Promise(resolve => setTimeout(resolve, 300))
  }
  throw new Error(`La bomba ${pumpId} no confirmó isRunning=${running}.`)
}

const login = await call('/auth/login', undefined, 'POST', { email: process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local', password })
const token = login.accessToken
const energy = await call('/energy/status', token)
const history = await call('/energy/history?hours=1', token)
if (!energy.reading || history.length === 0) throw new Error('No se persistió telemetría energética.')
const tanks = await call('/water-supply/status', token)
const pump = tanks.flatMap(tank => tank.pumps).find(item => !item.hasUnacknowledgedFault)
if (!pump) throw new Error('No hay bomba disponible para el smoke.')
if (pump.isRunning) { await call(`/water-supply/pumps/${pump.id}/stop`, token, 'POST', { reason: 'Normalización previa smoke Sprint 4' }); await waitForPump(token, pump.id, false) }
await call(`/water-supply/pumps/${pump.id}/start`, token, 'POST', { reason: 'Smoke MQTT Sprint 4' })
const started = await waitForPump(token, pump.id, true)
await call(`/water-supply/pumps/${pump.id}/stop`, token, 'POST', { reason: 'Cierre smoke Sprint 4' })
const stopped = await waitForPump(token, pump.id, false)
const dashboard = await call('/week14/dashboard?days=30', token)
console.log(JSON.stringify({ stationTelemetry: started.lastTelemetryAtUtc != null, pressureBar: started.lastPressureBar, energyReadings: history.length, pumpStartAck: started.isRunning, pumpStopAck: !stopped.isRunning, consumptionSources: dashboard.bySource.map(item => item.source) }))
