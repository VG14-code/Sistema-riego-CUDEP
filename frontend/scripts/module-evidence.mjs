// Captura la evidencia visual de un módulo: entra con la cuenta indicada, abre la
// pantalla del menú lateral y guarda las capturas en docs/evidencia/img.
// Uso: node scripts/module-evidence.mjs <nombre-del-menu> <prefijo> [ancho] [alto]
import { mkdir } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'
import { signIn } from './login.mjs'

const [menu = 'Resumen operativo', prefijo = 'modulo', anchoArg, altoArg] = process.argv.slice(2)
const baseUrl = process.env.VISUAL_BASE_URL ?? 'http://localhost:5173'
const email = process.env.VISUAL_EMAIL
const password = process.env.VISUAL_PASSWORD
const totpSecret = process.env.VISUAL_TOTP_SECRET
const salida = path.resolve(process.env.VISUAL_OUTPUT_DIR ?? path.join(process.cwd(), '..', 'docs', 'evidencia', 'img'))
const ancho = Number(anchoArg ?? 1440)
const alto = Number(altoArg ?? 900)

if (!email || !password) throw new Error('Define VISUAL_EMAIL y VISUAL_PASSWORD.')

await mkdir(salida, { recursive: true })
const navegador = await chromium.launch()
const contexto = await navegador.newContext({ viewport: { width: ancho, height: alto }, deviceScaleFactor: 1, locale: 'es-GT' })
const page = await contexto.newPage()
try {
  await page.goto(baseUrl, { waitUntil: 'domcontentloaded' })
  await signIn(page, { email, password, totpSecret })
  await page.getByRole('button', { name: menu, exact: false }).first().click()
  await page.waitForTimeout(4000)
  const completa = path.join(salida, `${prefijo}-completo.png`)
  await page.screenshot({ path: completa, fullPage: true })
  const visible = path.join(salida, `${prefijo}-vista.png`)
  await page.screenshot({ path: visible })
  await page.setViewportSize({ width: 375, height: 812 })
  await page.waitForTimeout(1500)
  const movil = path.join(salida, `${prefijo}-movil.png`)
  await page.screenshot({ path: movil, fullPage: true })
  console.log(JSON.stringify({ completa, visible, movil }, null, 1))
} finally {
  await contexto.close()
  await navegador.close()
}
