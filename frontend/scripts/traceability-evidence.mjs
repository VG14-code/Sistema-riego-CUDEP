import { chmod, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'
import { signIn } from './login.mjs'

const baseUrl = process.env.VISUAL_BASE_URL ?? 'http://localhost:5173'
const email = process.env.VISUAL_EMAIL
const password = process.env.VISUAL_PASSWORD
const totpSecret = process.env.VISUAL_TOTP_SECRET
const outputDir = path.resolve(process.env.VISUAL_OUTPUT_DIR ?? path.join(process.cwd(), '..', 'artifacts', 'trazabilidad'))
if (!email || !password) throw new Error('Define VISUAL_EMAIL y VISUAL_PASSWORD.')
await mkdir(outputDir, { recursive: true })
const userDataDir = await mkdtemp(path.join(tmpdir(), 'sistema-riego-trazabilidad-'))
await chmod(userDataDir, 0o777).catch(() => undefined)
const captures = []
let context
try {
  context = await chromium.launchPersistentContext(userDataDir, { headless: true, viewport: { width: 1680, height: 1050 }, args: ['--no-sandbox', '--disable-gpu'] })
  const page = context.pages()[0] ?? await context.newPage()
  await page.goto(baseUrl, { waitUntil: 'networkidle', timeout: 45_000 })
  await signIn(page, { email, password, totpSecret })
  const capture = async (file) => {
    await page.waitForTimeout(900)
    const target = path.join(outputDir, file)
    await page.screenshot({ path: target, fullPage: true })
    captures.push(target)
  }
  const navigate = async (label, file) => {
    const button = page.locator('.w2-app > aside nav button').filter({ hasText: label }).first()
    await button.click()
    await page.waitForFunction(text => Array.from(document.querySelectorAll('.w2-app > aside nav button')).some(item => item.classList.contains('active') && item.textContent?.includes(text)), label)
    await capture(file)
  }
  const tab = async (selector, label, file) => {
    const button = page.locator(selector).filter({ hasText: label }).first()
    await button.click()
    await page.waitForTimeout(500)
    await capture(file)
  }

  await capture('m01-s01-s04-inicio-operativo.png')
  await navigate('Finca', 'm02-s05-s10-territorio-suelos.png')
  await navigate('Catálogos', 'm02-s11-s24-catalogos.png')
  await tab('.s2-tabs button', 'Marcas', 'm02-s17-marcas.png')
  await tab('.s2-tabs button', 'Modelos', 'm02-s17-modelos.png')
  await navigate('Usuarios', 'm03-s25-s32-usuarios-seguridad-sesiones.png')
  await navigate('Seguridad 2FA', 'm03-s29-s30-autenticacion-2fa.png')
  await navigate('Auditoría', 'm03-s32-autorizaciones-bitacora.png')
  await navigate('Red IoT', 'm04-s33-s39-red-iot.png')
  await tab('.iot-tabs button', 'Nodos', 'm04-s35-s36-comunicacion-heartbeat.png')
  await tab('.iot-tabs button', 'Trazabilidad', 'm04-s34-s39-trazabilidad-iot.png')
  await navigate('Lecturas', 'm05-s41-s46-telemetria-historial-calidad.png')
  await navigate('Red IoT', 'm05-s40-configuracion-sensores.png')
  await tab('.iot-tabs button', 'Calibración', 'm05-s45-calibracion.png')
  await navigate('Agronomía', 'm06-s47-s51-gestion-agronomica.png')
  await navigate('Cultivos', 'm07-s52-s56-planificacion-calendario-rotacion.png')

  const report = path.join(outputDir, 'capture-report.json')
  await writeFile(report, JSON.stringify({ generatedAtUtc: new Date().toISOString(), baseUrl, captures }, null, 2))
  console.log(JSON.stringify({ count: captures.length, captures, report }, null, 2))
} finally {
  await context?.close()
  await rm(userDataDir, { recursive: true, force: true }).catch(() => undefined)
}