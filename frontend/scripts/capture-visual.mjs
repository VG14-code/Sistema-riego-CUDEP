import { chmod, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'

const baseUrl = process.env.VISUAL_BASE_URL ?? 'http://localhost:5173'
const email = process.env.VISUAL_EMAIL
const password = process.env.VISUAL_PASSWORD
const outputDir = path.resolve(process.env.VISUAL_OUTPUT_DIR ?? path.join(process.cwd(), '..', 'artifacts', 'visual-validation'))

let fixtureId = 1
const fixtureTime = (secondsAgo) => new Date(Date.now() - secondsAgo * 1000).toISOString()
const commandAlerts = (count, command, startSecondsAgo) => Array.from({ length: count }, (_, index) => ({
  id: fixtureId++,
  type: 'Comando MQTT sin confirmar',
  severity: 'Crítica',
  status: 'Activa',
  origin: 'Motor de alertas',
  description: `El dispositivo Válvula solenoide A1 no respondió al comando ${command} dentro del tiempo esperado.\nDetalle técnico: Comando MQTT expirado sin confirmación del dispositivo.`,
  relatedEntityType: 'Válvula solenoide',
  relatedEntityId: 'VALVULA-A1',
  relatedEntityName: 'Válvula solenoide A1 · VALVULA-A1',
  raisedAtUtc: fixtureTime(startSecondsAgo + index * 20),
  escalationLevel: 1,
}))
const referenceAlerts = [
  ...commandAlerts(23, 'ABRIR_VALVULA', 10),
  ...commandAlerts(4, 'CONSULTAR_ESTADO', 90),
  ...Array.from({ length: 7 }, (_, index) => ({
    id: fixtureId++,
    type: 'Condición crítica',
    severity: 'Crítica',
    status: 'Activa',
    origin: 'Motor de alertas',
    description: `La condición crítica ${index + 1} requiere atención del operador.`,
    relatedEntityType: 'Dispositivo IoT',
    relatedEntityId: `CRITICO-${index + 1}`,
    relatedEntityName: `Equipo crítico ${index + 1}`,
    raisedAtUtc: fixtureTime(600 + index * 60),
    escalationLevel: 1,
  })),
  ...[0, 0, 1, 1, 2].map((group, index) => ({
    id: fixtureId++,
    type: 'Advertencia operativa',
    severity: 'Advertencia',
    status: 'Activa',
    origin: 'Motor de alertas',
    description: `La condición de advertencia ${group + 1} requiere revisión.`,
    relatedEntityType: 'Sensor',
    relatedEntityId: `ADVERTENCIA-${group + 1}`,
    relatedEntityName: `Sensor de control ${group + 1}`,
    raisedAtUtc: fixtureTime(1200 + index * 60),
    escalationLevel: 0,
  })),
]
const referenceSupply = [{
  id: 'tanque-visual',
  name: 'Tanque principal CUDEP',
  capacityLiters: 10000,
  currentLevelLiters: 7200,
  levelPercent: 72,
  minimumSafePercent: 15,
  maximumFillPercent: 95,
  status: 'Disponible',
  lastLevelReadingUtc: fixtureTime(5),
  capacityValveLimit: 3,
  pumps: [{
    id: 'bomba-visual',
    name: 'Bomba de abastecimiento 1',
    status: 'Esperando ACK de parada',
    isRunning: true,
    maximumRunMinutes: 45,
    minimumRestMinutes: 10,
    startedAtUtc: fixtureTime(600),
    lastStoppedAtUtc: fixtureTime(7200),
    lockedUntilUtc: null,
    failureReason: null,
    hasUnacknowledgedFault: false,
    lastPressureBar: 3.2,
    lastMotorCurrentAmps: 8.2,
    lastTelemetryAtUtc: fixtureTime(5),
    ratedFlowLitersMinute: 25,
    minimumPressureBar: 1.5,
  }],
}]

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
  await page.route('**/api/alerts', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(referenceAlerts) }))
  await page.route('**/api/water-supply/status', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(referenceSupply) }))
  await page.route('**/api/water-supply/history**', route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))

  await page.locator('input[type="email"]').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: /iniciar sesión/i }).click()
  await page.locator('.w2-app').waitFor({ timeout: 30_000 })
  await page.getByText('TELEMETRÍA EN VIVO', { exact: true }).waitFor({ timeout: 30_000 })
  await page.locator('.s1-readings article').first().waitFor({ timeout: 30_000 })

  const dashboardPath = path.join(outputDir, 'dashboard-module-1.png')
  await page.screenshot({ path: dashboardPath, fullPage: true })

  const notificationPanel = page.locator('.n-center aside')
  if (!await notificationPanel.isVisible()) await page.getByRole('button', { name: 'Centro de notificaciones' }).click()
  await notificationPanel.waitFor({ timeout: 10_000 })
  await page.locator('.n-mini-list article.crítica').first().waitFor({ timeout: 10_000 })
  const visualChecks = await page.evaluate(() => {
    const panel = document.querySelector('.n-center aside')
    const card = document.querySelector('.n-mini-list article.crítica')
    const title = card?.querySelector('.n-group-heading > b')
    const button = card?.querySelector(':scope > button')
    const renderedMessages = Array.from(document.querySelectorAll('.n-mini-list article.crítica > span')).map(item => item.textContent)
    if (!panel || !card || !title || !button) throw new Error('El panel no contiene una tarjeta crítica completa para validar.')
    return {
      panelBackground: getComputedStyle(panel).backgroundColor,
      panelColor: getComputedStyle(panel).color,
      panelZIndex: getComputedStyle(document.querySelector('.n-center')).zIndex,
      cardColor: getComputedStyle(card).color,
      actionBackground: getComputedStyle(button).backgroundColor,
      actionColor: getComputedStyle(button).color,
      severityBadge: getComputedStyle(title, '::after').content,
      renderedMessages,
      realtimeStatus: document.querySelector('.n-realtime')?.textContent,
    }
  })
  if (visualChecks.panelBackground !== 'rgb(255, 255, 255)' || visualChecks.actionBackground === 'rgba(0, 0, 0, 0)' || !visualChecks.severityBadge.includes('CRÍTICA') || visualChecks.renderedMessages.some(message => /SignalR|negotiation/i.test(message ?? ''))) {
    throw new Error('Validación visual CSS falló: ' + JSON.stringify(visualChecks))
  }

  const module1Layout = await page.evaluate(() => {
    const panel = document.querySelector('.n-center aside')?.getBoundingClientRect()
    const selectors = ['.m-kpis', '.s1-dashboard-grid', '.s1-readings']
    const content = selectors.map(selector => ({ selector, rect: document.querySelector(selector)?.getBoundingClientRect().toJSON() }))
    if (!panel || content.some(item => !item.rect)) throw new Error('No se pudo medir el layout del Módulo 1.')
    return { panel: panel.toJSON(), content, centerClass: document.querySelector('.n-center')?.className, contentPaddingRight: getComputedStyle(document.querySelector('.w2-content')).paddingRight, viewportWidth: window.innerWidth, overlaps: content.filter(item => item.rect.right > panel.left).map(item => item.selector) }
  })
  if (module1Layout.overlaps.length > 0) throw new Error('El panel cubre contenido del Módulo 1: ' + JSON.stringify(module1Layout))

  const notificationsPath = path.join(outputDir, 'notifications-panel.png')
  await page.screenshot({ path: notificationsPath })

  await page.getByRole('button', { name: 'Bomba y tanque' }).click()
  await page.getByText('BOMBA MQTT · Esperando ACK de parada', { exact: true }).waitFor({ timeout: 15_000 })
  const stopButton = page.getByRole('button', { name: 'Detener' })
  await stopButton.waitFor({ state: 'visible', timeout: 10_000 })
  const module8Layout = await page.evaluate(() => {
    const panel = document.querySelector('.n-center aside')?.getBoundingClientRect()
    const pump = document.querySelector('.o-pump')?.getBoundingClientRect()
    const stop = Array.from(document.querySelectorAll('.o-pump button')).find(button => button.textContent?.trim() === 'Detener')
    const stopRect = stop?.getBoundingClientRect()
    if (!panel || !pump || !stop || !stopRect) throw new Error('No se pudo medir la tarjeta activa de la bomba.')
    const pointElement = document.elementFromPoint(stopRect.left + stopRect.width / 2, stopRect.top + stopRect.height / 2)
    return {
      panel: panel.toJSON(),
      pump: pump.toJSON(),
      stopButton: stopRect.toJSON(),
      pumpOverlapsPanel: pump.right > panel.left,
      stopButtonAccessible: pointElement === stop || stop.contains(pointElement),
    }
  })
  if (module8Layout.pumpOverlapsPanel || !module8Layout.stopButtonAccessible) {
    throw new Error('La bomba o su botón Detener quedan ocultos por el panel: ' + JSON.stringify(module8Layout))
  }

  const module8Path = path.join(outputDir, 'module-8-notifications-drawer.png')
  await page.screenshot({ path: module8Path })

  const moduleChecks = [
    ['Inicio', 'inicio'],
    ['Finca', 'territory'],
    ['Lecturas', 'telemetry'],
    ['Agronomía', 'agronomy'],
    ['Cultivos', 'planning'],
    ['Automatización', 'automation'],
    ['Bomba y tanque', 'supply'],
    ['Energía solar', 'energy'],
    ['Riego manual', 'manual'],
    ['Consumo', 'operations'],
    ['Alertas', 'alerts'],
    ['Mantenimiento', 'maintenance'],
    ['Administración', 'overview'],
    ['Usuarios', 'users'],
    ['Seguridad 2FA', 'security'],
    ['Catálogos', 'catalogs'],
    ['Red IoT', 'iot'],
    ['Parámetros', 'settings'],
    ['Auditoría', 'audit'],
  ]
  const moduleLayouts = {}
  const verificationPaths = {}

  const measureCurrentModule = async (navigationName, moduleId) => {
    const navButton = page.locator('.w2-app > aside nav button').filter({ hasText: navigationName }).first()
    await navButton.click()
    await page.waitForFunction(
      ({ label }) => Array.from(document.querySelectorAll('.w2-app > aside nav button')).some(button => button.classList.contains('active') && button.textContent?.trim().includes(label)),
      { label: navigationName },
    )
    await page.waitForTimeout(700)

    const result = await page.evaluate(({ label, id }) => {
      const panelElement = document.querySelector('.n-center aside')
      const contentElement = document.querySelector('.w2-content')
      const panel = panelElement?.getBoundingClientRect()
      const content = contentElement?.getBoundingClientRect()
      if (!panel || !content || !contentElement) throw new Error('No se pudo medir el layout global de ' + label + '.')

      const isVisible = element => {
        const style = getComputedStyle(element)
        const rect = element.getBoundingClientRect()
        return style.display !== 'none' && style.visibility !== 'hidden' && Number(style.opacity) > 0 && rect.width > 0 && rect.height > 0
      }
      const candidates = Array.from(contentElement.querySelectorAll('button,input,select,textarea,a,article,form,table,[class*="grid"],[class*="layout"],[class*="card"],[class*="item"]')).filter(isVisible)
      const overlapping = candidates.filter(element => {
        const rect = element.getBoundingClientRect()
        return rect.right > panel.left + 1 && rect.left < panel.right && rect.bottom > panel.top && rect.top < panel.bottom
      }).map(element => ({
        tag: element.tagName.toLowerCase(),
        className: typeof element.className === 'string' ? element.className : '',
        text: element.textContent?.trim().replace(/\s+/g, ' ').slice(0, 90),
        right: element.getBoundingClientRect().right,
      })).slice(0, 12)
      const inaccessible = candidates.filter(element => {
        if (!element.matches('button,input,select,textarea,a')) return false
        const rect = element.getBoundingClientRect()
        if (rect.bottom <= 0 || rect.top >= innerHeight || rect.right <= 0 || rect.left >= innerWidth) return false
        const x = Math.min(Math.max(rect.left + rect.width / 2, 0), innerWidth - 1)
        const y = Math.min(Math.max(rect.top + rect.height / 2, 0), innerHeight - 1)
        const hit = document.elementFromPoint(x, y)
        return Boolean(hit && hit !== element && !element.contains(hit) && !hit.contains(element))
      }).map(element => {
        const rect = element.getBoundingClientRect()
        const hit = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2)
        return {
          tag: element.tagName.toLowerCase(),
          className: typeof element.className === 'string' ? element.className : '',
          text: element.textContent?.trim().replace(/\s+/g, ' ').slice(0, 90),
          hitTag: hit?.tagName.toLowerCase(),
          hitClass: typeof hit?.className === 'string' ? hit.className : '',
        }
      }).slice(0, 12)

      return {
        id,
        label,
        panel: panel.toJSON(),
        content: content.toJSON(),
        contentClass: contentElement.className,
        reservedGap: panel.left - content.right,
        overlapping,
        inaccessible,
        ok: content.right <= panel.left && overlapping.length === 0 && inaccessible.length === 0,
      }
    }, { label: navigationName, id: moduleId })

    if (!result.ok) throw new Error('El drawer global falla en ' + navigationName + ': ' + JSON.stringify(result))
    return result
  }

  for (const [navigationName, moduleId] of moduleChecks) {
    moduleLayouts[moduleId] = await measureCurrentModule(navigationName, moduleId)
    if (moduleId === 'territory' || moduleId === 'telemetry' || moduleId === 'settings') {
      const screenshotPath = path.join(outputDir, `global-drawer-${moduleId}.png`)
      await page.screenshot({ path: screenshotPath })
      verificationPaths[moduleId] = screenshotPath
    }
  }

  const reportPath = path.join(outputDir, 'capture-report.json')
  await writeFile(reportPath, JSON.stringify({ generatedAtUtc: new Date().toISOString(), baseUrl, dashboardPath, notificationsPath, module8Path, verificationPaths, visualChecks, module1Layout, module8Layout, moduleLayouts }, null, 2))

  console.log(JSON.stringify({ dashboardPath, notificationsPath, module8Path, verificationPaths, reportPath, userDataDir, visualChecks, module1Layout, module8Layout, moduleLayouts }, null, 2))
} finally {
  await context?.close()
  await new Promise(resolve => setTimeout(resolve, 300))
  await rm(userDataDir, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 })
    .catch(error => console.warn('No se pudo limpiar el perfil temporal:', error.message))
}
