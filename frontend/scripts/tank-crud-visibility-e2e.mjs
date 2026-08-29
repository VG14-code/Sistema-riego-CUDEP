import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'
import { signIn } from './login.mjs'

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
 await signIn(page, { email, password, totpSecret })
 await page.locator('.w2-app > aside nav button').filter({hasText:'Bomba y tanque'}).click()
 const duplicateAccess=page.getByRole('button',{name:'Gestionar tanques',exact:true})
 const summary=page.getByRole('region',{name:'Acceso a gestión de tanques'})
 const form=page.getByTestId('tank-management-form')
 const catalog=page.getByRole('heading',{name:'Reservas administrables'})
 const tank=page.getByTestId('operational-tank').filter({hasText:'Tanque principal CUDEP'})
 await Promise.all([summary.waitFor(),form.waitFor(),catalog.waitFor(),tank.waitFor()])
 const contextualButton=summary.getByRole('button',{name:'Ocultar controles'})
 await contextualButton.waitFor()
 const screenshot=path.join(output,'modulo-11-crud-y-tanque-pagina-completa.png')
 await page.screenshot({path:screenshot,fullPage:true})
 const [summaryBox,formBox,tankBox]=await Promise.all([summary.boundingBox(),form.boundingBox(),tank.boundingBox()])
 result={
  usuario:await page.locator('.w2-profile').innerText(),
  botonSuperiorDuplicadoAusente:await duplicateAccess.count()===0,
  botonContextualVisible:await contextualButton.isVisible(),
  resumenGestionVisible:await summary.isVisible(),
  formularioVisible:await form.isVisible(),
  listaTanquesVisible:await catalog.isVisible(),
  tanqueOperativoVisible:await tank.isVisible(),
  tanquePrincipal:await tank.innerText(),
  posicionesEnPagina:{resumen:summaryBox,formulario:formBox,tanque:tankBox},
  captura:screenshot
 }
 if(!result.botonSuperiorDuplicadoAusente||!result.botonContextualVisible||!result.formularioVisible||!result.listaTanquesVisible||!result.tanqueOperativoVisible)throw new Error('La gestión y el tanque no quedaron visibles en la misma página.')
 await writeFile(path.join(output,'visibility-report.json'),JSON.stringify(result,null,2))
}finally{await browser?.close()}
console.log(JSON.stringify(result,null,2))