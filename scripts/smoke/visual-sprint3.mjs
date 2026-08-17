import playwright from '../../frontend/node_modules/playwright-core/index.js'
const { chromium } = playwright

const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
const frontendUrl = process.env.FRONTEND_URL ?? 'http://127.0.0.1:5173'
const browser = await chromium.launch({ headless: true, executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' })
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  const errors = []
  let hubOpened = false
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  page.on('websocket', socket => { if (socket.url().includes('/hubs/telemetry')) hubOpened = true })
  await page.goto(frontendUrl, { waitUntil: 'networkidle' })
  await page.locator('input[type=email]').fill(process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local')
  await page.locator('input[type=password]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()
  await page.locator('.w2-app').waitFor({ timeout: 15_000 })
  await page.getByText('Telemetría en vivo').waitFor({ timeout: 10_000 })
  const firstReading = await page.locator('.s1-readings article').first().textContent()
  await page.waitForTimeout(6_000)
  const nextReading = await page.locator('.s1-readings article').first().textContent()
  if (firstReading === nextReading) throw new Error('El dashboard no cambió tras una nueva trama MQTT/SignalR.')
  const polygons = await page.locator('.leaflet-overlay-pane svg path').count()
  if (polygons < 2) throw new Error(`Leaflet solo renderizó ${polygons} polígonos.`)
  await page.screenshot({ path: 'qa_planificacion/sprint3-dashboard-live.png', fullPage: true })

  await page.getByRole('button', { name: /Finca/ }).click()
  await page.locator('.tm-sector').first().waitFor({ timeout: 10_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint3-territory-polygons.png', fullPage: true })

  await page.getByRole('button', { name: /riego manual/i }).click()
  await page.getByRole('button', { name: 'PARO TOTAL' }).waitFor({ timeout: 10_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint3-emergency-stop.png', fullPage: true })
  const relevant = errors.filter(error => !error.includes('status of 404') && !error.includes('stopped during negotiation'))
  if (!hubOpened) relevant.push('SignalR no abrió WebSocket.')
  if (relevant.length) throw new Error(`Errores de página: ${relevant.join(' | ')}`)
  console.log(JSON.stringify({ dashboardRealtime: true, signalRWebSocket: true, leafletPolygons: polygons, emergencyStopVisible: true }))
} finally { await browser.close() }
