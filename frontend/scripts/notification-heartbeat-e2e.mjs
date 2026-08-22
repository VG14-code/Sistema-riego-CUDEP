import { chmod, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'
import signalR from '@microsoft/signalr'

const baseUrl = process.env.E2E_BASE_URL ?? 'http://localhost:5173'
const apiRoot = process.env.E2E_API_URL ?? 'http://localhost:5080/api'
const email = process.env.E2E_EMAIL ?? 'admin@sistemariego.local'
const password = process.env.E2E_PASSWORD
const outputDir = path.resolve(process.env.E2E_OUTPUT_DIR ?? path.join(process.cwd(), '..', 'artifacts', 'notification-e2e'))
if (!password) throw new Error('Define E2E_PASSWORD.')

const call = async (route, token, method = 'GET', body) => {
  const response = await fetch(`${apiRoot}${route}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const data = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error(`${method} ${route}: ${response.status} ${JSON.stringify(data)}`)
  return data
}
const waitFor = async (description, check, timeoutMs) => {
  const started = Date.now()
  while (Date.now() - started < timeoutMs) {
    const value = await check()
    if (value) return value
    await new Promise(resolve => setTimeout(resolve, 1000))
  }
  throw new Error(`Tiempo agotado esperando: ${description}`)
}

await mkdir(outputDir, { recursive: true })
const profileDir = await mkdtemp(path.join(tmpdir(), 'sistema-riego-notification-e2e-'))
await chmod(profileDir, 0o777).catch(() => undefined)
const report = {
  startedAtUtc: new Date().toISOString(),
  initialActiveCount: null,
  signalREvent: null,
  activeAfterDisconnect: [],
  toastVisible: false,
  panelAlertVisible: false,
  resolvedInApi: false,
  resolvedInUi: false,
  captures: {},
}

let context
let connection
try {
  const session = await call('/auth/login', undefined, 'POST', { email, password })
  const initialActive = await call('/alerts?status=Activa&take=1000', session.accessToken)
  report.initialActiveCount = initialActive.length
  if (initialActive.length !== 0) throw new Error(`La prueba exige 0 alertas activas al inicio; se encontraron ${initialActive.length}.`)

  context = await chromium.launchPersistentContext(profileDir, {
    headless: true,
    viewport: { width: 1600, height: 1000 },
    args: ['--no-sandbox', '--disable-gpu'],
  })
  const page = context.pages()[0] ?? await context.newPage()
  await page.goto(baseUrl, { waitUntil: 'networkidle', timeout: 45_000 })
  await page.locator('input[type="email"]').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()
  await page.locator('.w2-app').waitFor({ timeout: 30_000 })
  await page.getByRole('button', { name: 'Centro de notificaciones' }).click()
  await page.locator('.n-center aside').waitFor({ timeout: 10_000 })
  await page.locator('.n-empty').waitFor({ timeout: 10_000 })
  await page.getByRole('button', { name: /ver historial completo/i }).waitFor({ timeout: 10_000 })
  await page.locator('.n-realtime.online').waitFor({ timeout: 15_000 })
  const initialPath = path.join(outputDir, '01-initial-empty.png')
  await page.screenshot({ path: initialPath })
  report.captures.initial = initialPath

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${apiRoot.replace(/\/api$/, '')}/hubs/telemetry`, { accessTokenFactory: () => session.accessToken })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.on('telemetryReadingReceived', () => undefined)
  connection.on('alertResolved', () => undefined)

  let resolveAlert
  let rejectAlert
  const alertRaised = new Promise((resolve, reject) => { resolveAlert = resolve; rejectAlert = reject })
  const signalTimeout = setTimeout(() => rejectAlert(new Error('SignalR no emitió alertRaised en 90 segundos.')), 90_000)
  connection.on('alertRaised', alert => {
    if (alert?.type === 'Conexión' && alert?.status === 'Activa') {
      clearTimeout(signalTimeout)
      resolveAlert(alert)
    }
  })
  await connection.start()
  console.log('READY ' + JSON.stringify({ initialActiveCount: report.initialActiveCount, initialPath }))

  const signalAlert = await alertRaised
  report.signalREvent = signalAlert
  await page.locator('.n-toast').waitFor({ state: 'visible', timeout: 6_000 })
  report.toastVisible = true
  await page.locator('.n-mini-list article').first().waitFor({ state: 'visible', timeout: 10_000 })
  report.panelAlertVisible = true
  const raisedPath = path.join(outputDir, '02-heartbeat-alert-and-toast.png')
  await page.screenshot({ path: raisedPath })
  report.captures.raised = raisedPath

  report.activeAfterDisconnect = await waitFor(
    'alertas activas persistidas',
    async () => {
      const alerts = await call('/alerts?status=Activa&take=1000', session.accessToken)
      const connectionAlerts = alerts.filter(alert => alert.type === 'Conexión')
      return connectionAlerts.length > 0 ? connectionAlerts : null
    },
    15_000,
  )
  console.log('ALERT_OBSERVED ' + JSON.stringify({
    signalRAlertId: signalAlert.id,
    activeIds: report.activeAfterDisconnect.map(alert => alert.id),
    toastVisible: report.toastVisible,
    panelAlertVisible: report.panelAlertVisible,
    raisedPath,
  }))

  await waitFor(
    'auto-resolución de las alertas en API',
    async () => {
      const active = await call('/alerts?status=Activa&take=1000', session.accessToken)
      return report.activeAfterDisconnect.every(alert => !active.some(item => item.id === alert.id))
    },
    120_000,
  )
  report.resolvedInApi = true
  console.log('API_RESOLVED ' + JSON.stringify({ ids: report.activeAfterDisconnect.map(alert => alert.id) }))

  try {
    await page.locator('.n-empty').waitFor({ state: 'visible', timeout: 15_000 })
    report.resolvedInUi = true
  } catch {
    report.resolvedInUi = false
  }
  const resolvedPath = path.join(outputDir, '03-reconnected-state.png')
  await page.screenshot({ path: resolvedPath })
  report.captures.resolved = resolvedPath
  report.finishedAtUtc = new Date().toISOString()
  const reportPath = path.join(outputDir, 'notification-heartbeat-report.json')
  await writeFile(reportPath, JSON.stringify(report, null, 2))
  console.log('COMPLETE ' + JSON.stringify({ reportPath, ...report }))
  if (!report.resolvedInUi) throw new Error('La API resolvió las alertas, pero el panel no volvió al estado vacío.')
} finally {
  await connection?.stop().catch(() => undefined)
  await context?.close()
  await rm(profileDir, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 }).catch(() => undefined)
}