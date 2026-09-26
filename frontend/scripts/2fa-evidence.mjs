// Captura la pantalla de alta del segundo factor (QR y clave manual) para la
// evidencia del modulo 3. Solo sirve con una cuenta que aun no tenga 2FA activo.
// La clave compartida se difumina antes de guardar la imagen.
import { chromium } from 'playwright'
import { signIn } from './login.mjs'
const nav = await chromium.launch()
const ctx = await nav.newContext({ viewport: { width: 1440, height: 900 }, locale: 'es-GT' })
const page = await ctx.newPage()
await page.goto('http://localhost:5173', { waitUntil: 'domcontentloaded' })
await signIn(page, { email: process.env.VISUAL_EMAIL, password: process.env.VISUAL_PASSWORD })
await page.getByRole('button', { name: /Seguridad/i }).first().click()
await page.waitForTimeout(600)
await page.getByRole('button', { name: /Doble autenticaci/i }).first().click()
await page.waitForTimeout(2500)
await page.getByRole('button', { name: /Generar c.digo QR/i }).click()
await page.locator('main svg, main img, section svg').first().waitFor({ timeout: 20000 })
await page.waitForTimeout(1500)
// La clave del autenticador no debe quedar legible en la evidencia.
await page.evaluate(() => {
  const clave = [...document.querySelectorAll('main *')].find(e => /^[a-z0-9]{4}( [a-z0-9]{4}){3,}$/i.test(e.textContent.trim()) && e.children.length === 0)
  if (clave) { clave.style.filter = 'blur(6px)'; clave.style.userSelect = 'none' }
})
console.log('texto:', (await page.locator('body').innerText()).slice(-260))
await page.screenshot({ path: '../docs/evidencia/img/modulo-03-2fa-vista.png' })
await page.screenshot({ path: '../docs/evidencia/img/modulo-03-2fa-completo.png', fullPage: true })
console.log('QR capturado')
await ctx.close(); await nav.close()
