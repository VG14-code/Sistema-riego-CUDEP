import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const webRoot=process.env.E2E_BASE_URL??'http://localhost:5173'
const apiRoot=process.env.E2E_API_URL??'http://localhost:5080/api'
const email=process.env.E2E_EMAIL??'admin@sistemariego.local'
const password=process.env.E2E_PASSWORD
if(!password)throw new Error('Define E2E_PASSWORD.')
const output=path.resolve(process.cwd(),'..','artifacts','tank-crud-e2e')
await mkdir(output,{recursive:true})
const call=async(route,token,method='GET',body)=>{
 const response=await fetch(apiRoot+route,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:body===undefined?undefined:JSON.stringify(body)})
 const data=response.status===204?null:await response.json().catch(()=>null)
 if(!response.ok)throw new Error(method+' '+route+': '+response.status+' '+JSON.stringify(data))
 return data
}
const session=await call('/auth/login',undefined,'POST',{email,password})
const result={}
let browser,secondName,originalMain
try{
 browser=await chromium.launch({headless:true,args:['--no-sandbox','--disable-gpu']})
 const page=await browser.newPage({viewport:{width:1600,height:1000}})
 page.on('dialog',dialog=>dialog.accept())
 await page.goto(webRoot,{waitUntil:'networkidle',timeout:45000})
 await page.locator('input[type="email"]').fill(email)
 await page.locator('input[type="password"]').fill(password)
 await page.getByRole('button',{name:/iniciar sesión/i}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 await page.locator('.w2-app > aside nav button').filter({hasText:'Bomba y tanque'}).click()
 const form=page.getByTestId('tank-management-form')
 await form.waitFor({timeout:15000})
 result.formularioGestionVisible=true
 const originalTanks=await call('/water-supply/tanks',session.accessToken)
 originalMain=originalTanks.find(t=>t.name==='Tanque principal CUDEP')
 await form.getByLabel('Nombre').fill('Validación visible')
 await form.getByLabel('Mínimo seguro (%)').fill('95')
 await form.getByLabel('Máximo de llenado (%)').fill('20')
 await form.getByRole('button',{name:'Agregar tanque'}).click()
 const validationMessage=form.getByRole('alert')
 await validationMessage.waitFor()
 result.validacionVisible=await validationMessage.textContent()

 const suffix=new Date().toISOString().replace(/\D/g,'').slice(0,14)
 secondName='Tanque experimental UI '+suffix
 await form.getByLabel('Nombre').fill(secondName)
 await form.getByLabel('Capacidad (L)').fill('4500')
 await form.getByLabel('Nivel actual (L)').fill('2800')
 await form.getByLabel('Mínimo seguro (%)').fill('20')
 await form.getByLabel('Máximo de llenado (%)').fill('88')
 await form.getByRole('button',{name:'Agregar tanque'}).click()
 let secondCatalog=page.getByTestId('tank-catalog-item').filter({hasText:secondName})
 await secondCatalog.waitFor({timeout:15000})
 let activeCards=page.getByTestId('operational-tank')
 await activeCards.filter({hasText:secondName}).waitFor({timeout:15000})
 const mainCard=activeCards.filter({hasText:'Tanque principal CUDEP'})
 await mainCard.waitFor()
 result.segundoTanqueCreado={nombre:secondName,capacidadLitros:4500,minimo:20,maximo:88}
 result.dosTanquesSimultaneos=(await activeCards.count())>=2
 const twoTanksPath=path.join(output,'01-dos-tanques-configurados.png')
 await page.screenshot({path:twoTanksPath,fullPage:true})

 const mainCatalog=page.getByTestId('tank-catalog-item').filter({hasText:'Tanque principal CUDEP'})
 await mainCatalog.getByRole('button',{name:'Editar'}).click()
 await form.getByLabel('Capacidad (L)').fill('11000')
 await form.getByLabel('Mínimo seguro (%)').fill('25')
 await form.getByLabel('Máximo de llenado (%)').fill('93')
 await form.getByRole('button',{name:'Guardar cambios'}).click()
 await mainCard.getByText('Mínimo 25%',{exact:true}).waitFor({timeout:15000})
 await mainCard.getByText(/de 11,000 L/).waitFor()
 result.tanquePrincipalEditado={capacidadLitros:11000,minimo:25,maximo:93}
 const editedPath=path.join(output,'02-tanque-principal-editado.png')
 await page.screenshot({path:editedPath,fullPage:true})

 await mainCatalog.getByRole('button',{name:'Editar'}).click()
 const level=Number(await form.getByLabel('Nivel actual (L)').inputValue())
 const capacity=Number(await form.getByLabel('Capacidad (L)').inputValue())
 const levelPercent=level/capacity*100
 const protectionMinimum=Math.min(92,Math.max(26,Math.ceil(levelPercent)+1))
 await form.getByLabel('Mínimo seguro (%)').fill(String(protectionMinimum))
 await form.getByLabel('Máximo de llenado (%)').fill('93')
 await form.getByRole('button',{name:'Guardar cambios'}).click()
 await mainCard.getByText(`Mínimo ${protectionMinimum}%`,{exact:true}).waitFor({timeout:15000})
 const pumpButton=mainCard.getByRole('button',{name:'Encender'})
 await pumpButton.waitFor({timeout:10000})
 await pumpButton.click()
 const protectionMessage=page.getByText('Arranque bloqueado: nivel bajo y riesgo de trabajo en seco.',{exact:true}).first()
 await protectionMessage.waitFor({timeout:10000})
 result.proteccionReal={verificada:true,nivelPorcentaje:Number(levelPercent.toFixed(1)),minimoEnsayado:protectionMinimum,mensaje:await protectionMessage.textContent()}
 const protectionPath=path.join(output,'03-proteccion-umbral-real.png')
 await page.screenshot({path:protectionPath,fullPage:true})

 await mainCatalog.getByRole('button',{name:'Editar'}).click()
 await form.getByLabel('Mínimo seguro (%)').fill('25')
 await form.getByRole('button',{name:'Guardar cambios'}).click()
 await mainCard.getByText('Mínimo 25%',{exact:true}).waitFor({timeout:15000})

 secondCatalog=page.getByTestId('tank-catalog-item').filter({hasText:secondName})
 await secondCatalog.getByRole('button',{name:'Desactivar'}).click()
 await secondCatalog.getByText('Inactivo',{exact:true}).waitFor({timeout:15000})
 await activeCards.filter({hasText:secondName}).waitFor({state:'detached',timeout:15000})
 result.segundoTanqueDesactivado=true
 result.desaparecioVistaOperativa=await activeCards.filter({hasText:secondName}).count()===0
 const inactivePath=path.join(output,'04-segundo-tanque-desactivado.png')
 await page.screenshot({path:inactivePath,fullPage:true})

 result.capturas=[twoTanksPath,editedPath,protectionPath,inactivePath]
}finally{
 await browser?.close()
 const tanks=await call('/water-supply/tanks',session.accessToken)
 for(const tank of tanks.filter(item=>item.name===secondName)){
  if(tank.status!=='Inactivo')await call('/water-supply/tanks/'+tank.id+'/deactivate',session.accessToken,'PATCH')
  await call('/water-supply/tanks/'+tank.id,session.accessToken,'DELETE')
 }
 if(originalMain)await call('/water-supply/tanks/'+originalMain.id,session.accessToken,'PUT',{
  name:originalMain.name,
  capacityLiters:originalMain.capacityLiters,
  currentLevelLiters:originalMain.currentLevelLiters,
  minimumSafePercent:originalMain.minimumSafePercent,
  maximumFillPercent:originalMain.maximumFillPercent,
  pumpIds:originalMain.pumps.map(pump=>pump.id),
  isActive:originalMain.status!=='Inactivo',
 })
 result.datosTemporalesEliminados=true
 await writeFile(path.join(output,'tank-crud-e2e-report.json'),JSON.stringify(result,null,2))
}
console.log(JSON.stringify(result,null,2))