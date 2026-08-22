import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const webRoot=process.env.E2E_BASE_URL??'http://localhost:5173'
const email=process.env.E2E_EMAIL??'admin@sistemariego.local'
const password=process.env.E2E_PASSWORD
if(!password)throw new Error('Define E2E_PASSWORD.')
const output=path.resolve(process.cwd(),'..','artifacts','pump-ack-20260822')
await mkdir(output,{recursive:true})
let browser
const result={erroresConsola:[]}
try{
  browser=await chromium.launch({headless:true,args:['--no-sandbox','--disable-gpu']})
  const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1})
  page.on('console',message=>{if(message.type()==='error')result.erroresConsola.push(message.text())})
  await page.goto(webRoot,{waitUntil:'networkidle',timeout:45000})
  await page.locator('input[type="email"]').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button',{name:/iniciar sesión/i}).click()
  await page.locator('.w2-app').waitFor({timeout:30000})
  await page.locator('.w2-app > aside nav button').filter({hasText:'Bomba y tanque'}).click()

  const management=page.getByRole('region',{name:'Acceso a gestión de tanques'})
  await management.waitFor()
  const duplicateButtons=page.getByRole('button',{name:'Gestionar tanques',exact:true})
  result.botonesGestionarTanques=await duplicateButtons.count()
  if(result.botonesGestionarTanques!==0)throw new Error('Todavía existe el botón redundante Gestionar tanques.')
  const contextual=management.getByRole('button',{name:'Ocultar controles'})
  await contextual.waitFor()
  result.botonContextual=await contextual.innerText()
  await contextual.click()

  const pump=page.locator('.o-pump').filter({hasText:'Bomba de abastecimiento 1'})
  await pump.waitFor({timeout:20000})
  const startButton=pump.getByRole('button',{name:'Encender'})
  await startButton.waitFor()
  await startButton.click()
  await pump.getByRole('button',{name:'Detener'}).waitFor({timeout:20000})
  await page.waitForFunction(()=>{const card=[...document.querySelectorAll('.o-pump')].find(x=>x.textContent?.includes('Bomba de abastecimiento 1'));const text=card?.textContent??'';const current=Number((text.match(/corriente ([\d.,]+) A/)?.[1]??'0').replace(',','.'));return text.includes('Presión 3.2 bar')&&current>0},undefined,{timeout:20000})
  result.encendido=await pump.innerText()
  const onShot=path.join(output,'01-bomba-encendida-ack.png')
  await page.screenshot({path:onShot,fullPage:true})
  await page.waitForTimeout(5500)

  await pump.getByRole('button',{name:'Detener'}).click()
  await pump.getByRole('button',{name:'Encender'}).waitFor({timeout:20000})
  await page.waitForFunction(()=>{const card=[...document.querySelectorAll('.o-pump')].find(x=>x.textContent?.includes('Bomba de abastecimiento 1'));return card?.textContent?.includes('BOMBA MQTT · Detenida')},undefined,{timeout:20000})
  const history=page.locator('.o-panel').filter({hasText:'Ciclos de abastecimiento'})
  const latest=history.locator('.o-row').first()
  await latest.waitFor()
  await page.waitForFunction(()=>{const panel=[...document.querySelectorAll('.o-panel')].find(x=>x.textContent?.includes('Ciclos de abastecimiento'));const row=panel?.querySelector('.o-row');const text=row?.textContent??'';const liters=Number((text.match(/([\d.,]+)\s*L/)?.[1]??'0').replace(',','.'));return text.includes('Completado')&&liters>0},undefined,{timeout:20000})
  result.detenido=await pump.innerText()
  result.cicloNuevo=await latest.innerText()
  const liters=Number((result.cicloNuevo.match(/([\d.,]+)\s*L/)?.[1]??'0').replace(',','.'))
  if(!(liters>0))throw new Error('El ciclo nuevo terminó sin volumen suministrado.')

  const rows=await history.locator('.o-row').allInnerTexts()
  result.ciclosVisibles=rows
  result.huerfanosEnCursoCero=rows.filter(text=>text.includes('En curso')&&/\\b0(?:[.,]0)?\\s*L\\b/.test(text)).length
  if(result.huerfanosEnCursoCero!==0)throw new Error('Todavía hay ciclos huérfanos En curso · 0 L entre los ciclos visibles.')

  await page.getByRole('button',{name:'Centro de notificaciones'}).click()
  const drawer=page.locator('.n-center.open aside')
  await drawer.waitFor()
  const drawerText=await drawer.innerText()
  result.alertaTimeoutActiva=/no respondió al comando (ENCENDER_BOMBA|APAGAR_BOMBA)/i.test(drawerText)
  if(result.alertaTimeoutActiva)throw new Error('Se generó una alerta de falta de ACK durante la operación normal.')
  result.centroNotificaciones=drawerText
  const offShot=path.join(output,'02-bomba-detenida-ciclo-completo.png')
  await page.screenshot({path:offShot,fullPage:true})

  result.capturas=[onShot,offShot]
  result.operacionNormalSinAlertasTimeout=true
  await writeFile(path.join(output,'pump-ack-report.json'),JSON.stringify(result,null,2))
}finally{
  await browser?.close()
}
console.log(JSON.stringify(result,null,2))