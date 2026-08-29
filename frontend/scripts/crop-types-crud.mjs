import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'
import { signIn } from './login.mjs'

const baseUrl = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173'
const apiRoot = process.env.E2E_API_URL ?? 'http://127.0.0.1:5080/api'
const email = process.env.E2E_EMAIL ?? 'admin@sistemariego.local'
const password = process.env.E2E_PASSWORD
const totpSecret = process.env.E2E_TOTP_SECRET
const temporaryCode = 'TEMP_E2E_CULTIVO'
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

const session = await call('/auth/login', undefined, 'POST', { email, password })
let browser
let result = {}
try {
  browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--disable-gpu'] })
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } })
  await page.goto(baseUrl, { waitUntil: 'networkidle' })
  await signIn(page, { email, password, totpSecret })

  const navigate = async name => {
    await page.locator('.w2-app > aside nav button').filter({ hasText: name }).click()
    await page.waitForTimeout(150)
  }
  const openCropTypeCatalog = async () => {
    await navigate('Catálogos')
    await page.getByRole('button', { name: 'Tipos de cultivo', exact: true }).click()
    await page.locator('.s2-table tbody tr').first().waitFor()
  }
  const cropTypeRow = text => page.locator('.s2-table tbody tr').filter({ hasText: text })
  const agronomyDropdown = async () => {
    await navigate('Agronomía')
    const section = page.locator('.s2-panel').filter({ has: page.getByRole('heading', { name: 'Cultivos', exact: true }) })
    const dropdown = section.getByLabel('Tipo de cultivo')
    await dropdown.locator('option').first().waitFor({ state: 'attached' })
    return dropdown
  }

  await openCropTypeCatalog()
  const initialCatalogCount = await page.locator('.s2-table tbody tr').count()
  await page.getByLabel('Código').fill(temporaryCode)
  await page.getByLabel('Nombre').fill('Temporal E2E')
  await page.getByRole('button', { name: 'Agregar', exact: true }).click()
  let row = cropTypeRow(temporaryCode)
  await row.waitFor()
  let dropdown = await agronomyDropdown()
  const optionsAfterCreate = await dropdown.locator('option').allTextContents()

  await openCropTypeCatalog()
  row = cropTypeRow(temporaryCode)
  await row.getByRole('button', { name: 'Editar' }).click()
  await page.getByLabel('Nombre').fill('Temporal validado E2E')
  await page.getByRole('button', { name: 'Actualizar', exact: true }).click()
  row = cropTypeRow('Temporal validado E2E')
  await row.waitFor()
  await mkdir('../artifacts/visual-validation', { recursive: true })
  const screenshot = '../artifacts/visual-validation/crop-types-crud.png'
  await page.screenshot({ path: screenshot, fullPage: true })
  dropdown = await agronomyDropdown()
  const optionsAfterEdit = await dropdown.locator('option').allTextContents()

  await openCropTypeCatalog()
  row = cropTypeRow('Temporal validado E2E')
  await row.getByRole('button', { name: 'Desactivar' }).click()
  await row.getByText('Inactivo', { exact: true }).waitFor()
  dropdown = await agronomyDropdown()
  const optionsAfterDisable = await dropdown.locator('option').allTextContents()

  result = {
    initialCatalogCount,
    createdVisibleInDropdown: optionsAfterCreate.includes('Temporal E2E'),
    editedVisibleInDropdown: optionsAfterEdit.includes('Temporal validado E2E'),
    disabledRemovedFromDropdown: !optionsAfterDisable.includes('Temporal validado E2E'),
    screenshot,
  }
  if (initialCatalogCount !== 10 || !result.createdVisibleInDropdown || !result.editedVisibleInDropdown || !result.disabledRemovedFromDropdown)
    throw new Error(`El CRUD o su reflejo inmediato fallaron: ${JSON.stringify(result)}`)
} finally {
  await browser?.close()
  const types = await call('/agronomy/crop-types', session.accessToken)
  for (const item of types.filter(type => type.code === temporaryCode))
    await call(`/agronomy/crop-types/${item.id}`, session.accessToken, 'DELETE')
}

console.log(JSON.stringify(result, null, 2))