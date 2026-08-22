import { createHash, createHmac, randomBytes } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'

const webRoot=process.env.E2E_BASE_URL??'http://localhost:5173'
const email=process.env.E2E_EMAIL??'admin@sistemariego.local'
const originalPassword=process.env.E2E_PASSWORD
if(!originalPassword)throw new Error('Define E2E_PASSWORD.')
const recoveredPassword='Rec!9a'+randomBytes(10).toString('hex')
const output=path.resolve(process.cwd(),'..','artifacts','password-recovery')
const mailbox=path.resolve(process.cwd(),'..','backend','SistemaRiego.Api','dev-mailbox')
await mkdir(output,{recursive:true})
await mkdir(mailbox,{recursive:true})

const sqlcmd='C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE'
function sql(query){
 return execFileSync(sqlcmd,['-S','(localdb)\\MSSQLLocalDB','-d','SistemaRiego','-W','-h','-1','-Q','SET NOCOUNT ON; '+query],{encoding:'utf8',windowsHide:true}).trim()
}
function decodeBase32(value){
 const alphabet='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567',clean=value.replace(/\s|-/g,'').toUpperCase();let bits='',bytes=[]
 for(const char of clean){const index=alphabet.indexOf(char);if(index<0)throw new Error('Secreto TOTP inválido.');bits+=index.toString(2).padStart(5,'0')}
 for(let index=0;index+8<=bits.length;index+=8)bytes.push(parseInt(bits.slice(index,index+8),2))
 return Buffer.from(bytes)
}
function totp(secret){
 const counter=Math.floor(Date.now()/30000),buffer=Buffer.alloc(8);buffer.writeBigUInt64BE(BigInt(counter))
 const digest=createHmac('sha1',decodeBase32(secret)).update(buffer).digest(),offset=digest[digest.length-1]&15
 return String((digest.readUInt32BE(offset)&0x7fffffff)%1000000).padStart(6,'0')
}
async function newestMail(after,excluded=new Set()){
 const deadline=Date.now()+15000
 do{
  const candidates=[]
  for(const name of await readdir(mailbox)){if(!name.endsWith('.html')||excluded.has(name))continue;const file=path.join(mailbox,name),info=await stat(file);if(info.mtimeMs>=after-1000)candidates.push({name,file,time:info.mtimeMs})}
  if(candidates.length){candidates.sort((a,b)=>b.time-a.time);const selected=candidates[0],html=await readFile(selected.file,'utf8'),match=html.match(/href="([^"]+)"/);if(!match)throw new Error('El correo no contiene enlace.');return {...selected,link:match[1].replaceAll('&amp;','&')}}
  await new Promise(resolve=>setTimeout(resolve,300))
 }while(Date.now()<deadline)
 throw new Error('No se generó el correo de desarrollo.')
}
const tokenFromLink=link=>new URL(link).searchParams.get('resetToken')
const tokenHash=token=>createHash('sha256').update(token).digest('hex').toUpperCase()
const secret=sql("SELECT [Value] FROM AspNetUserTokens t JOIN AspNetUsers u ON u.Id=t.UserId WHERE u.NormalizedEmail='ADMIN@SISTEMARIEGO.LOCAL' AND t.Name='AuthenticatorKey';")
if(!secret)throw new Error('El administrador no tiene clave TOTP configurada.')

const profile=await mkdtemp(path.join(os.tmpdir(),'riego-password-e2e-'))
const report={steps:[],captures:[]}
const generatedMails=new Set()
const recoveryHashes=new Set()
let context
try{
 context=await chromium.launchPersistentContext(profile,{headless:true,viewport:{width:1600,height:1000},args:['--no-sandbox','--disable-gpu']})
 const page=await context.newPage()
 await page.goto(webRoot,{waitUntil:'networkidle',timeout:45000})
 await page.getByRole('button',{name:'¿Olvidaste tu contraseña?'}).waitFor()
 const loginCapture=path.join(output,'01-enlace-recuperacion-login.png');await page.screenshot({path:loginCapture,fullPage:true});report.captures.push(loginCapture);report.steps.push('1. Enlace visible en login: OK')

 await page.getByRole('button',{name:'¿Olvidaste tu contraseña?'}).click()
 await page.getByLabel('Correo electrónico').fill(email)
 const validStarted=Date.now()
 await page.getByRole('button',{name:'Enviar enlace'}).click()
 const validMessage=await page.getByRole('status').textContent()
 const validMail=await newestMail(validStarted);generatedMails.add(validMail.name)
 const validToken=tokenFromLink(validMail.link);if(!validToken)throw new Error('Token ausente en el correo válido.');recoveryHashes.add(tokenHash(validToken))
 report.steps.push('2. Correo de desarrollo generado para cuenta válida: OK')

 await page.getByRole('button',{name:/Volver al inicio/}).click()
 await page.getByRole('button',{name:'¿Olvidaste tu contraseña?'}).click()
 await page.getByLabel('Correo electrónico').fill('cuenta-inexistente@sistemariego.local')
 await page.getByRole('button',{name:'Enviar enlace'}).click()
 const missingMessage=await page.getByRole('status').textContent()
 if(validMessage!==missingMessage)throw new Error('Las respuestas válida/inexistente no son idénticas.')
 const identicalCapture=path.join(output,'02-respuesta-no-revelacion.png');await page.screenshot({path:identicalCapture,fullPage:true});report.captures.push(identicalCapture);report.steps.push('3. Correo inexistente recibe respuesta idéntica: OK')

 await page.goto(validMail.link,{waitUntil:'networkidle'})
 await page.getByLabel('Nueva contraseña',{exact:true}).fill(recoveredPassword)
 await page.getByLabel('Confirmar nueva contraseña').fill(recoveredPassword)
 await page.getByRole('button',{name:'Guardar nueva contraseña'}).click()
 await page.getByText('Contraseña actualizada. Ya puedes iniciar sesión.').waitFor()
 await page.getByLabel('Correo electrónico').fill(email)
 await page.getByLabel('Contraseña').fill(recoveredPassword)
 await page.getByRole('button',{name:'Iniciar sesión'}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 report.steps.push('4. Enlace usado, contraseña cambiada e inicio de sesión correcto: OK')

 await page.evaluate(()=>sessionStorage.clear())
 await page.goto(validMail.link,{waitUntil:'networkidle'})
 await page.getByLabel('Nueva contraseña',{exact:true}).fill('OtraClave9!Segura')
 await page.getByLabel('Confirmar nueva contraseña').fill('OtraClave9!Segura')
 await page.getByRole('button',{name:'Guardar nueva contraseña'}).click()
 await page.getByText(/inválido|utilizado|expiró/i).waitFor()
 const reuseCapture=path.join(output,'03-enlace-reutilizado-rechazado.png');await page.screenshot({path:reuseCapture,fullPage:true});report.captures.push(reuseCapture);report.steps.push('5. Reutilización del enlace rechazada: OK')

 await new Promise(resolve=>setTimeout(resolve,61000))
 await page.getByRole('button',{name:/Volver al inicio/}).click()
 await page.getByRole('button',{name:'¿Olvidaste tu contraseña?'}).click()
 await page.getByLabel('Correo electrónico').fill(email)
 const expiredStarted=Date.now()
 await page.getByRole('button',{name:'Enviar enlace'}).click()
 await page.getByRole('status').waitFor()
 const expiredMail=await newestMail(expiredStarted,generatedMails);generatedMails.add(expiredMail.name)
 const expiredToken=tokenFromLink(expiredMail.link);if(!expiredToken)throw new Error('Token ausente en correo para expiración.');const expiredHash=tokenHash(expiredToken);recoveryHashes.add(expiredHash)
 sql("UPDATE PasswordRecoveryTokens SET ExpiresAtUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE TokenHash='"+expiredHash+"';")
 await page.goto(expiredMail.link,{waitUntil:'networkidle'})
 await page.getByLabel('Nueva contraseña',{exact:true}).fill('Expirada9!Segura')
 await page.getByLabel('Confirmar nueva contraseña').fill('Expirada9!Segura')
 await page.getByRole('button',{name:'Guardar nueva contraseña'}).click()
 await page.getByText(/inválido|utilizado|expiró/i).waitFor()
 const expiredCapture=path.join(output,'04-enlace-expirado-rechazado.png');await page.screenshot({path:expiredCapture,fullPage:true});report.captures.push(expiredCapture);report.steps.push('6. Token expirado rechazado: OK')

 await page.getByRole('button',{name:/Volver al inicio/}).click()
 await page.getByLabel('Correo electrónico').fill(email)
 await page.getByLabel('Contraseña').fill(recoveredPassword)
 await page.getByRole('button',{name:'Iniciar sesión'}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 await page.locator('.w2-app > aside nav button').filter({hasText:'Usuarios'}).click()
 const row=page.locator('.s2-table tbody tr').filter({hasText:email})
 await row.waitFor()
 let dialogIndex=0
 page.on('dialog',async dialog=>{dialogIndex++;if(dialog.type()==='confirm')await dialog.accept();else if(dialog.type()==='prompt')await dialog.accept(totp(secret));else await dialog.dismiss()})
 await row.getByRole('button',{name:'Restablecer contraseña'}).click()
 const modal=page.getByRole('dialog')
 await modal.waitFor({timeout:15000})
 report.steps.push('7. Restablecimiento administrativo desde Usuarios con TOTP: OK')
 const temporary=await modal.getByTestId('temporary-password').textContent();if(!temporary)throw new Error('No se mostró contraseña temporal.')
 await modal.getByTestId('temporary-password').evaluate(element=>{element.textContent='•••••••• · valor ocultado en la evidencia'})
 const adminCapture=path.join(output,'05-password-temporal-una-vez.png');await page.screenshot({path:adminCapture,fullPage:true});report.captures.push(adminCapture)
 await modal.getByRole('button',{name:'Entendido, cerrar'}).click()
 if(await page.getByRole('dialog').count())throw new Error('La contraseña temporal siguió visible después de cerrar.')
 report.steps.push('8. Contraseña temporal mostrada una sola vez y retirada al cerrar: OK')

 await page.evaluate(()=>sessionStorage.clear())
 await page.reload({waitUntil:'networkidle'})
 await page.getByLabel('Correo electrónico').fill(email)
 await page.getByLabel('Contraseña').fill(temporary)
 await page.getByRole('button',{name:'Iniciar sesión'}).click()
 await page.getByRole('heading',{name:'Crea tu contraseña definitiva'}).waitFor({timeout:15000})
 const forcedCapture=path.join(output,'06-cambio-obligatorio-primer-login.png');await page.screenshot({path:forcedCapture,fullPage:true});report.captures.push(forcedCapture)
 await page.getByLabel('Contraseña temporal').fill(temporary)
 await page.getByLabel('Nueva contraseña',{exact:true}).fill(originalPassword)
 await page.getByLabel('Confirmar nueva contraseña').fill(originalPassword)
 await page.getByRole('button',{name:'Cambiar contraseña y continuar'}).click()
 await page.locator('.w2-app').waitFor({timeout:30000})
 report.steps.push('9. Login temporal bloqueado hasta cambiar contraseña; contraseña original restaurada: OK')
 await page.getByRole('button',{name:'Cerrar sesión'}).click()
 await page.getByRole('button',{name:'Iniciar sesión'}).waitFor({timeout:15000})
 report.testSessionClosed=true
 report.totpDialogCount=dialogIndex
 report.finalPasswordRestored=true
 report.identicalResponse=validMessage
 await writeFile(path.join(output,'e2e-report.json'),JSON.stringify(report,null,2))
}finally{
 await context?.close()
 for(let attempt=0;attempt<5;attempt++){try{await rm(profile,{recursive:true,force:true});break}catch(error){if(attempt===4)console.error("No se pudo borrar el perfil temporal: "+error.message);await new Promise(resolve=>setTimeout(resolve,500))}}
 if(recoveryHashes.size)sql("DELETE FROM PasswordRecoveryTokens WHERE TokenHash IN ("+[...recoveryHashes].map(value=>"'"+value+"'").join(',')+");")
}
console.log(JSON.stringify(report,null,2))