import { createHmac } from 'node:crypto'

const baseUrl = process.env.API_URL ?? 'http://localhost:5080/api'
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
const decodeBase32 = value => {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'; let bits = ''
  for (const char of value.replace(/[^A-Z2-7]/gi, '').toUpperCase()) bits += alphabet.indexOf(char).toString(2).padStart(5, '0')
  const bytes = []; for (let i = 0; i + 8 <= bits.length; i += 8) bytes.push(Number.parseInt(bits.slice(i, i + 8), 2))
  return Buffer.from(bytes)
}
const totp = secret => {
  const buffer = Buffer.alloc(8); buffer.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)))
  const digest = createHmac('sha1', decodeBase32(secret)).update(buffer).digest(); const offset = digest.at(-1) & 15
  return (((digest[offset] & 127) << 24 | digest[offset + 1] << 16 | digest[offset + 2] << 8 | digest[offset + 3]) % 1_000_000).toString().padStart(6, '0')
}
const call = async (path, token, method = 'GET', body, code) => {
  const response = await fetch(`${baseUrl}${path}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(code ? { 'X-TOTP-Code': code } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const data = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${JSON.stringify(data)}`)
  return data
}
const waitForStatus = async (token, id, status) => {
  for (let attempt = 0; attempt < 30; attempt++) {
    const run = (await call('/manual-irrigation/runs', token)).find(item => item.id === id)
    if (run?.status === status) return run
    await new Promise(resolve => setTimeout(resolve, 300))
  }
  throw new Error(`El riego ${id} no alcanzó estado ${status}.`)
}

const login = await call('/auth/login', undefined, 'POST', { email: process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local', password })
const token = login.accessToken
const setup = await call('/security/2fa/setup', token, 'POST'); const secret = setup.sharedKey
await call('/security/2fa/enable', token, 'POST', { code: totp(secret) })
try {
  const zone = (await call('/manual-irrigation/zones', token)).find(item => item.hasValve && !item.isRunning)
  if (!zone) throw new Error('No hay una zona con válvula disponible.')
  const started = await call('/manual-irrigation/start', token, 'POST', { irrigationZoneId: zone.id, durationMinutes: 2, flowRateLitersMinute: 12, reason: 'Smoke MQTT Sprint 3', observations: 'Validación correlacionada' }, totp(secret))
  await waitForStatus(token, started.id, 'En curso')
  await call(`/manual-irrigation/${started.id}/stop`, token, 'POST', { observations: 'Cierre smoke' })
  const stopped = await waitForStatus(token, started.id, 'Detenido')
  console.log(JSON.stringify({ runId: started.id, openAck: true, closeAck: true, status: stopped.status }))
} finally {
  await call('/security/2fa/disable', token, 'POST', { code: totp(secret) })
}
