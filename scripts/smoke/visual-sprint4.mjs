import playwright from '../../frontend/node_modules/playwright-core/index.js'
const password = process.env.SMOKE_PASSWORD
if (!password) throw new Error('SMOKE_PASSWORD no está configurada.')
const browser = await playwright.chromium.launch({ headless: true, executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' })
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  await page.goto(process.env.FRONTEND_URL ?? 'http://127.0.0.1:5173', { waitUntil: 'networkidle' })
  await page.locator('input[type=email]').fill(process.env.SMOKE_EMAIL ?? 'admin@sistemariego.local')
  await page.locator('input[type=password]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()
  await page.locator('.w2-app').waitFor({ timeout: 15_000 })
  await page.getByRole('button', { name: /energía solar/i }).click()
  await page.getByText('Autonomía energética').waitFor({ timeout: 10_000 })
  if (await page.locator('.recharts-responsive-container').count() !== 1) throw new Error('Recharts no renderizó la serie energética.')
  await page.screenshot({ path: 'qa_planificacion/sprint4-energy.png', fullPage: true })
  await page.getByRole('button', { name: /bomba y tanque/i }).click()
  await page.getByText(/Presión .* bar/).waitFor({ timeout: 10_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint4-water-station.png', fullPage: true })
  await page.getByRole('button', { name: /Consumo/i }).click()
  await page.getByText('Origen del consumo').waitFor({ timeout: 10_000 })
  await page.getByText('Comparación por cultivo').waitFor({ timeout: 10_000 })
  await page.screenshot({ path: 'qa_planificacion/sprint4-consumption.png', fullPage: true })
  const relevant = errors.filter(error => !error.includes('status of 404') && !error.includes('stopped during negotiation'))
  if (relevant.length) throw new Error(relevant.join(' | '))
  console.log(JSON.stringify({ energyChart: true, waterStationTelemetry: true, consumptionComparisons: true, pageErrors: 0 }))
} finally { await browser.close() }
