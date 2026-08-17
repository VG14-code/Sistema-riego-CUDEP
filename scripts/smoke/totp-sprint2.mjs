import { createHmac } from 'node:crypto'

const baseUrl = process.env.API_URL ?? 'http://localhost:5080/api'
const email = process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local'
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')

const decodeBase32 = value => {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
  let bits = ''
  for (const char of value.replace(/[^A-Z2-7]/gi, '').toUpperCase()) bits += alphabet.indexOf(char).toString(2).padStart(5, '0')
  const bytes = []
  for (let i = 0; i + 8 <= bits.length; i += 8) bytes.push(Number.parseInt(bits.slice(i, i + 8), 2))
  return Buffer.from(bytes)
}
const totp = secret => {
  const counter = Math.floor(Date.now() / 30_000)
  const buffer = Buffer.alloc(8); buffer.writeBigUInt64BE(BigInt(counter))
  const digest = createHmac('sha1', decodeBase32(secret)).update(buffer).digest()
  const offset = digest[digest.length - 1] & 15
  return (((digest[offset] & 127) << 24 | digest[offset + 1] << 16 | digest[offset + 2] << 8 | digest[offset + 3]) % 1_000_000).toString().padStart(6, '0')
}
const call = async (path, token, method = 'GET', body, code) => {
  const response = await fetch(`${baseUrl}${path}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(code ? { 'X-TOTP-Code': code } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const data = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${JSON.stringify(data)}`)
  return data
}

const login = await call('/auth/login', undefined, 'POST', { email, password })
const token = login.accessToken
const setup = await call('/security/2fa/setup', token, 'POST')
const secret = setup.sharedKey
await call('/security/2fa/enable', token, 'POST', { code: totp(secret) })
try {
  const status = await call('/security/2fa/status', token)
  if (!status.enabled) throw new Error('2FA no quedó habilitado.')
  const roles = await call('/roles', token)
  const admin = roles.find(role => role.name === 'Administrador')
  if (!admin) throw new Error('No se encontró el rol Administrador.')
  await call('/roles/Administrador/permissions', token, 'PUT', { permissions: admin.permissions }, totp(secret))
  console.log('OK TOTP activado y operación crítica verificada con UserManager.')
} finally {
  await call('/security/2fa/disable', token, 'POST', { code: totp(secret) })
}
