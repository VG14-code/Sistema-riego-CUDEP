import signalR from '../../frontend/node_modules/@microsoft/signalr/dist/cjs/index.js'
const root = process.env.API_URL ?? 'http://localhost:5080/api'
const hubRoot = root.replace(/\/api$/, '')
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
async function call(path, token, method = 'GET', body) {
  const response = await fetch(`${root}${path}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const data = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${JSON.stringify(data)}`)
  return data
}
const login = await call('/auth/login', undefined, 'POST', { email: process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local', password })
const token = login.accessToken
const connection = new signalR.HubConnectionBuilder().withUrl(`${hubRoot}/hubs/telemetry`, { accessTokenFactory: () => token }).build()
const received = new Promise((resolve, reject) => {
  const timeout = setTimeout(() => reject(new Error('SignalR no emitió alertRaised.')), 10_000)
  connection.on('alertRaised', alert => { clearTimeout(timeout); resolve(alert) })
})
await connection.start()
const created = await call('/alerts/manual', token, 'POST', { type: 'Falla de dispositivo', severity: 'Crítica', description: 'Smoke Sprint 5: validación de alerta centralizada', relatedEntityType: 'Bomba', relatedEntityId: 'SMOKE-SPRINT5' })
const realtime = await received
await call(`/alerts/${created.id}/acknowledge`, token, 'POST')
const acknowledged = (await call('/alerts?status=Reconocida', token)).find(item => item.id === created.id)
if (!acknowledged?.acknowledgedAtUtc) throw new Error('El ACK de alerta no persistió usuario/fecha.')
const plan = await call('/maintenance/plans', token, 'POST', { name: 'Plan smoke Sprint 5', frequency: 'Único', intervalDays: null, equipmentType: 'Bomba', equipmentId: 'SMOKE-SPRINT5', scheduledAtUtc: new Date(Date.now() + 86400000).toISOString(), assignedToUserId: null, assignedToEmail: 'admin@sistemariego.local', notes: 'Validación E2E', status: 'Pendiente' })
const activity = await call('/maintenance/activities', token, 'POST', { maintenancePlanId: plan.id, title: 'Inspección smoke', equipmentType: 'Bomba', equipmentId: 'SMOKE-SPRINT5', scheduledAtUtc: new Date().toISOString(), assignedToUserId: null, assignedToEmail: 'admin@sistemariego.local', notes: 'E2E', status: 'Pendiente' })
const incident = await call('/maintenance/incidents', token, 'POST', { title: 'Incidencia smoke', description: 'Validación CRUD', equipmentType: 'Bomba', equipmentId: 'SMOKE-SPRINT5', severity: 'Advertencia', assignedToUserId: null, assignedToEmail: 'admin@sistemariego.local', notes: 'E2E', status: 'Pendiente' })
await call(`/maintenance/incidents/${incident.id}`, token, 'PUT', { status: 'Resuelta', assignedToUserId: null, assignedToEmail: 'admin@sistemariego.local', notes: 'Resuelta durante smoke' })
const history = await call('/maintenance/equipment/Bomba/SMOKE-SPRINT5/history', token)
await connection.stop()
console.log(JSON.stringify({ alertSignalR: realtime.id === created.id, acknowledged: acknowledged.status, plans: history.plans.length, activities: history.activities.length, incidents: history.incidents.length, incidentResolved: history.incidents.some(item => item.status === 'Resuelta'), activityId: activity.id }))
