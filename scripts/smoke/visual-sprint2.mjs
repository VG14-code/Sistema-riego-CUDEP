import playwright from '../../frontend/node_modules/playwright-core/index.js'
const { chromium } = playwright

const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
const browser = await chromium.launch({ headless: true, executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' })
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  const errors = []
  let hubOpened = false
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  page.on('websocket', socket => { if (socket.url().includes('/hubs/telemetry')) hubOpened = true })
  await page.goto(process.env.FRONTEND_URL ?? 'http://127.0.0.1:5174', { waitUntil: 'networkidle' })
  await page.locator('input[type=email]').fill(process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local')
  await page.locator('input[type=password]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()
  await page.locator('.w2-app').waitFor({ timeout: 15_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint2-dashboard.png', fullPage: true })
  await page.getByRole('button', { name: /cultivos/i }).click()
  await page.locator('.fc').waitFor()
  await page.screenshot({ path: 'qa_planificacion/sprint2-calendar.png', fullPage: true })
  await page.locator('aside nav button').filter({ hasText: 'Usuarios' }).click({ timeout: 10_000 })
  await page.getByText('Matriz rol × permiso').waitFor({ timeout: 10_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint2-security.png', fullPage: true })
  const relevantErrors = errors.filter(error => !error.includes('status of 404') && !error.includes('stopped during negotiation'))
  if (!hubOpened) relevantErrors.push('SignalR no abrió WebSocket.')
  if (relevantErrors.length) throw new Error(`Errores de página: ${relevantErrors.join(' | ')}`)
  console.log('OK dashboard, FullCalendar y matriz de permisos renderizados sin errores de página.')
} finally {
  await browser.close()
}
