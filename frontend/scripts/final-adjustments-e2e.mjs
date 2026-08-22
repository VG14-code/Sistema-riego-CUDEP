import { createHmac } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const webRoot=process.env.E2E_BASE_URL??'http://localhost:5173'
const apiRoot=process.env.E2E_API_URL??'http://localhost:5080/api'
const email=process.env.E2E_EMAIL??'admin@sistemariego.local'
const password=process.env.E2E_PASSWORD
if(!password)throw new Error('Define E2E_PASSWORD.')
const output=path.resolve(process.cwd(),'..','artifacts','final-validation')
await mkdir(output,{recursive:true})

async function call(route,token,method='GET',body,totp){
 const response=await fetch(apiRoot+route,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...(totp?{'X-TOTP-Code':totp}:{})},body:body===undefined?undefined:JSON.stringify(body)})
 const data=response.status===204?null:await response.json().catch(()=>null)
 if(!response.ok)throw new Error(method+' '+route+': '+response.status+' '+JSON.stringify(data))
 return data
}
function decodeBase32(value){
 const alphabet='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567',clean=value.replace(/\s|-/g,'').toUpperCase()
 let bits='',bytes=[]
 for(const char of clean){const index=alphabet.indexOf(char);if(index<0)throw new Error('Secreto base32 inválido.');bits+=index.toString(2).padStart(5,'0')}
 for(let i=0;i+8<=bits.length;i+=8)bytes.push(parseInt(bits.slice(i,i+8),2))
 return Buffer.from(bytes)
}
function totp(secret){
 const counter=Math.floor(Date.now()/30000),buffer=Buffer.alloc(8);buffer.writeBigUInt64BE(BigInt(counter))
 const digest=createHmac('sha1',decodeBase32(secret)).update(buffer).digest(),offset=digest[digest.length-1]&15
 return String((digest.readUInt32BE(offset)&0x7fffffff)%1000000).padStart(6,'0')
}

const session=await call('/auth/login',undefined,'POST',{email,password})
const result={}
let browser
try{
 browser=await chromium.launch({headless:true,args:['--no-sandbox','--disable-gpu']})
 const page=await browser.newPage({viewport:{width:1600,height:1000}})
 page.on('dialog',dialog=>dialog.accept())
 await page.goto(webRoot,{waitUntil:'networkidle',timeout:45000})
 await page.locator('input[type="email"]').fill(email)
 await page.locator('input[type="password"]').fill(password)
 await page.getByRole('button',{name:/iniciar sesión/i}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 const navigate=async name=>{await page.locator('.w2-app > aside nav button').filter({hasText:name}).click();await page.waitForTimeout(600)}

 await navigate('Seguridad 2FA')
 await page.getByRole('button',{name:'Generar código QR'}).click()
 const qr=page.getByAltText('Código QR para configurar TOTP')
 await qr.waitFor({timeout:10000})
 const uri=await page.getByRole('link',{name:'Abrir configuración en este dispositivo'}).getAttribute('href')
 if(!uri?.startsWith('otpauth://totp/'))throw new Error('La URI otpauth no es estándar.')
 const secret=new URL(uri).searchParams.get('secret')
 if(!secret)throw new Error('La URI otpauth no contiene secreto.')
 const qrPath=path.join(output,'2fa-qr-setup.png')
 await page.screenshot({path:qrPath,fullPage:true})
 let code=totp(secret)
 await page.getByLabel('Código generado por la aplicación').fill(code)
 await page.getByRole('button',{name:'Confirmar y activar'}).click()
 await page.getByRole('heading',{name:'2FA está activo'}).waitFor({timeout:10000})
 const roles=await call('/roles',session.accessToken)
 const role=roles.find(item=>item.name===session.user.roles[0])??roles[0]
 await call('/roles/'+encodeURIComponent(role.name)+'/permissions',session.accessToken,'PUT',{permissions:role.permissions},totp(secret))
 code=totp(secret)
 await page.getByLabel('Código vigente para desactivar').fill(code)
 await page.getByRole('button',{name:'Desactivar 2FA'}).click()
 await page.getByRole('heading',{name:'Configura tu autenticador'}).waitFor({timeout:10000})
 const finalStatus=await call('/security/2fa/status',session.accessToken)

 await navigate('Inicio')
 await page.locator('.leaflet-container').waitFor({timeout:20000})
 const mapPath=path.join(output,'territorio-real-cudep.png')
 await page.screenshot({path:mapPath,fullPage:true})

 await navigate('Bomba y tanque')
 await page.getByRole('heading',{name:'Configuración de reserva'}).waitFor({timeout:15000})
 const tanksPath=path.join(output,'tanques-crud-configurable.png')
 await page.screenshot({path:tanksPath,fullPage:true})

 await navigate('Alertas')
 const statusSelect=page.locator('.s5-hero select').nth(1)
 await statusSelect.selectOption({label:'Resuelta'})
 await page.locator('.s5-status.resuelta').first().waitFor({timeout:15000})
 const alertsPath=path.join(output,'alertas-estado-resuelta.png')
 await page.screenshot({path:alertsPath,fullPage:true})

 await navigate('Inicio')
 await page.getByRole('button',{name:'Centro de notificaciones'}).click()
 await page.locator('.n-center aside').waitFor()
 const notificationsPath=path.join(output,'notificaciones-activas.png')
 await page.screenshot({path:notificationsPath})

 result.qrPath=qrPath;result.mapPath=mapPath;result.tanksPath=tanksPath;result.alertsPath=alertsPath;result.notificationsPath=notificationsPath
 result.otpauthStandard=true;result.enabled=true;result.criticalOperation='Matriz de permisos reenviada sin cambios con X-TOTP-Code válido';result.disabled=finalStatus.enabled===false
 if(!result.disabled)throw new Error('2FA no quedó desactivado al finalizar.')
 await writeFile(path.join(output,'e2e-report.json'),JSON.stringify(result,null,2))
}finally{await browser?.close()}
console.log(JSON.stringify(result,null,2))