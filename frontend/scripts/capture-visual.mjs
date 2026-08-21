import { chmod, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'

const baseUrl = process.env.VISUAL_BASE_URL ?? 'http://localhost:5173'
const email = process.env.VISUAL_EMAIL
const password = process.env.VISUAL_PASSWORD
const outputDir = path.resolve(process.env.VISUAL_OUTPUT_DIR ?? path.join(process.cwd(), '..', 'artifacts', 'visual-validation'))

if (!email || !password) {
  throw new Error('Define VISUAL_EMAIL y VISUAL_PASSWORD antes de ejecutar la captura.')
}

await mkdir(outputDir, { recursive: true })
const userDataDir = await mkdtemp(path.join(tmpdir(), 'sistema-riego-playwright-'))
await chmod(userDataDir, 0o777).catch(() => undefined)

let context
try {
  context = await chromium.launchPersistentContext(userDataDir, {
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
  await page.getByText('TELEMETRÍA EN VIVO', { exact: true }).waitFor({ timeout: 30_000 })
  await page.locator('.s1-readings article').first().waitFor({ timeout: 30_000 })

  const dashboardPath = path.join(outputDir, 'dashboard-module-1.png')
  await page.screenshot({ path: dashboardPath, fullPage: true })

  await page.getByRole('button', { name: 'Centro de notificaciones' }).click()
  await page.locator('.n-center aside').waitFor({ timeout: 10_000 })
  await page.locator('.n-summary').waitFor({ timeout: 10_000 })
  const visualChecks = await page.evaluate(() => {
    const panel = document.querySelector('.n-center aside')
    const card = document.querySelector('.n-mini-list article')
    const title = card?.querySelector('.n-group-heading > b')
    const button = card?.querySelector(':scope > button')
    if (!panel || !card || !title || !button) throw new Error('El panel no contiene una tarjeta completa para validar.')
    return {
      panelBackground: getComputedStyle(panel).backgroundColor,
      panelColor: getComputedStyle(panel).color,
      panelZIndex: getComputedStyle(document.querySelector('.n-center')).zIndex,
      cardColor: getComputedStyle(card).color,
      actionBackground: getComputedStyle(button).backgroundColor,
      actionColor: getComputedStyle(button).color,
      severityBadge: getComputedStyle(title, '::after').content,
    }
  })
  if (visualChecks.panelBackground !== 'rgb(255, 255, 255)' || visualChecks.actionBackground === 'rgba(0, 0, 0, 0)' || visualChecks.severityBadge === 'none') {
    throw new Error('Validación visual CSS falló: ' + JSON.stringify(visualChecks))
  }

  const notificationsPath = path.join(outputDir, 'notifications-panel.png')
  await page.screenshot({ path: notificationsPath })
  const reportPath = path.join(outputDir, 'capture-report.json')
  await writeFile(reportPath, JSON.stringify({ generatedAtUtc: new Date().toISOString(), baseUrl, dashboardPath, notificationsPath, visualChecks }, null, 2))

  console.log(JSON.stringify({ dashboardPath, notificationsPath, reportPath, userDataDir, visualChecks }, null, 2))
} finally {
  await context?.close()
  await new Promise(resolve => setTimeout(resolve, 300))
  await rm(userDataDir, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 })
    .catch(error => console.warn('No se pudo limpiar el perfil temporal:', error.message))
}
