import playwright from '../../frontend/node_modules/playwright-core/index.js'
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
const browser = await playwright.chromium.launch({ headless: true, executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' })
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } }); const errors = []
  page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  await page.goto(process.env.FRONTEND_URL ?? 'http://127.0.0.1:5173', { waitUntil: 'networkidle' })
  await page.locator('input[type=email]').fill(process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local'); await page.locator('input[type=password]').fill(password); await page.getByRole('button', { name: /iniciar sesión/i }).click(); await page.locator('.w2-app').waitFor({ timeout: 15_000 })
  await page.getByRole('button', { name: 'Centro de notificaciones' }).click(); await page.getByText('Alertas activas').waitFor(); await page.screenshot({ path: 'qa_planificacion/sprint5-notification-center.png', fullPage: true }); await page.getByRole('button', { name: 'Centro de notificaciones' }).click()
  await page.getByRole('button', { name: /Alertas/i }).click(); await page.getByText('Centro de alertas').waitFor(); await page.getByText('Condición detectada').first().waitFor(); await page.screenshot({ path: 'qa_planificacion/sprint5-alerts.png', fullPage: true })
  await page.getByRole('button', { name: /Mantenimiento/i }).click(); await page.getByText('Gestión del ciclo operativo').waitFor(); await page.getByText('Trabajo requerido').waitFor(); await page.screenshot({ path: 'qa_planificacion/sprint5-maintenance.png', fullPage: true })
  const relevant = errors.filter(error => !error.includes('status of 404') && !error.includes('stopped during negotiation'))
  if (relevant.length) throw new Error(relevant.join(' | '))
  console.log(JSON.stringify({ notificationCenter: true, alertOrigins: true, maintenancePanel: true, pageErrors: 0 }))
} finally { await browser.close() }
