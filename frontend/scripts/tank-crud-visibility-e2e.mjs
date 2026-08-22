import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const webRoot=process.env.E2E_BASE_URL??'http://localhost:5173'
const email=process.env.E2E_EMAIL??'admin@sistemariego.local'
const password=process.env.E2E_PASSWORD
if(!password)throw new Error('Define E2E_PASSWORD.')
const output=path.resolve(process.cwd(),'..','artifacts','tank-crud-visible')
await mkdir(output,{recursive:true})
let browser
let result={}
try{
 browser=await chromium.launch({headless:true,args:['--no-sandbox','--disable-gpu']})
 const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1})
 await page.goto(webRoot,{waitUntil:'networkidle',timeout:45000})
 await page.locator('input[type="email"]').fill(email)
 await page.locator('input[type="password"]').fill(password)
 await page.getByRole('button',{name:/iniciar sesión/i}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 await page.locator('.w2-app > aside nav button').filter({hasText:'Bomba y tanque'}).click()
 const access=page.getByTestId('tank-management-access')
 const summary=page.getByRole('region',{name:'Acceso a gestión de tanques'})
 const form=page.getByTestId('tank-management-form')
 const catalog=page.getByRole('heading',{name:'Reservas administrables'})
 const tank=page.getByTestId('operational-tank').filter({hasText:'Tanque principal CUDEP'})
 await Promise.all([access.waitFor(),summary.waitFor(),form.waitFor(),catalog.waitFor(),tank.waitFor()])
 const screenshot=path.join(output,'modulo-11-crud-y-tanque-pagina-completa.png')
 await page.screenshot({path:screenshot,fullPage:true})
 const [accessBox,formBox,tankBox]=await Promise.all([access.boundingBox(),form.boundingBox(),tank.boundingBox()])
 result={
  usuario:await page.locator('.w2-profile').innerText(),
  accesoGestionVisible:await access.isVisible(),
  resumenGestionVisible:await summary.isVisible(),
  formularioVisible:await form.isVisible(),
  listaTanquesVisible:await catalog.isVisible(),
  tanqueOperativoVisible:await tank.isVisible(),
  tanquePrincipal:await tank.innerText(),
  posicionesEnPagina:{acceso:accessBox,formulario:formBox,tanque:tankBox},
  captura:screenshot
 }
 if(!result.accesoGestionVisible||!result.formularioVisible||!result.listaTanquesVisible||!result.tanqueOperativoVisible)throw new Error('La gestión y el tanque no quedaron visibles en la misma página.')
 await writeFile(path.join(output,'visibility-report.json'),JSON.stringify(result,null,2))
}finally{await browser?.close()}
console.log(JSON.stringify(result,null,2))