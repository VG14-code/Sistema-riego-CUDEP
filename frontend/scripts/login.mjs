import { createHmac } from 'node:crypto'

// Las cuentas con segundo factor ya no entran solo con contraseña: el login
// entrega un desafío y la sesión se emite tras verificar el código. Los guiones
// del navegador calculan ese código igual que lo haría la aplicación
// autenticadora, a partir de la clave compartida que se generó en Seguridad 2FA.
const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'

const decodeBase32 = value => {
  const clean = String(value).replace(/[\s=-]/g, '').toUpperCase()
  if (!clean) throw new Error('La clave TOTP está vacía.')
  const bytes = []
  let bits = 0
  let accumulator = 0
  for (const character of clean) {
    const index = BASE32.indexOf(character)
    if (index < 0) throw new Error(`Carácter inválido en la clave base32: ${character}`)
    accumulator = (accumulator << 5) | index
    bits += 5
    if (bits < 8) continue
    bytes.push((accumulator >>> (bits - 8)) & 0xff)
    bits -= 8
  }
  return Buffer.from(bytes)
}

export const currentTotp = secret => {
  const counter = Buffer.alloc(8)
  counter.writeBigInt64BE(BigInt(Math.floor(Date.now() / 30000)))
  const hash = createHmac('sha1', decodeBase32(secret)).update(counter).digest()
  const offset = hash[hash.length - 1] & 0x0f
  const binary = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) | ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff)
  return String(binary % 1_000_000).padStart(6, '0')
}

// Inicia sesión y deja la página en el shell de la aplicación. Si la cuenta pide
// segundo factor, resuelve el paso de verificación con totpSecret.
export const signIn = async (page, { email, password, totpSecret }) => {
  await page.locator('input[type="email"]').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()

  const codeSelector = 'input[autocomplete="one-time-code"]'
  await page.waitForFunction(
    selector => Boolean(document.querySelector('.w2-app') || document.querySelector(selector)),
    codeSelector,
    { timeout: 30_000 })

  const code = page.locator(codeSelector)
  if (await code.count()) {
    if (!totpSecret) throw new Error(`La cuenta ${email} exige segundo factor. Define la clave del autenticador en la variable de entorno correspondiente (VISUAL_TOTP_SECRET o E2E_TOTP_SECRET) para que el guion genere el código.`)
    await code.fill(currentTotp(totpSecret))
    await page.getByRole('button', { name: /verificar código/i }).click()
  }
  await page.locator('.w2-app').waitFor({ timeout: 30_000 })
}
