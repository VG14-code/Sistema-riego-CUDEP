import { randomBytes, randomUUID } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { mkdir, readFile, readdir, stat } from 'node:fs/promises'
import path from 'node:path'

const apiRoot=process.env.E2E_API_URL??'http://localhost:5080/api'
const targetEmail='vmadridb@miumg.edu.gt'
const mailbox=path.resolve(process.cwd(),'..','backend','SistemaRiego.Api','dev-mailbox')
const sqlcmd='C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE'
await mkdir(mailbox,{recursive:true})

function sql(query){
 return execFileSync(sqlcmd,['-b','-S','(localdb)\\MSSQLLocalDB','-d','SistemaRiego','-W','-h','-1','-Q','SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; '+query],{encoding:'utf8',windowsHide:true}).trim()
}
async function call(route,method='GET',body){
 const response=await fetch(apiRoot+route,{method,headers:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)})
 const data=response.status===204?null:await response.json().catch(()=>null)
 if(!response.ok)throw new Error(method+' '+route+': '+response.status+' '+JSON.stringify(data))
 return {status:response.status,data}
}
async function newestMail(after){
 const deadline=Date.now()+45000
 do{
  const candidates=[]
  for(const name of await readdir(mailbox)){if(!name.endsWith('.html'))continue;const file=path.join(mailbox,name),info=await stat(file);if(info.mtimeMs>=after-1000)candidates.push({file,time:info.mtimeMs})}
  if(candidates.length){candidates.sort((a,b)=>b.time-a.time);const selected=candidates[0],html=await readFile(selected.file,'utf8'),match=html.match(/href="([^"]+)"/);if(!match)throw new Error('La copia SMTP no contiene enlace.');return {path:selected.file,link:match[1].replaceAll('&amp;','&')}}
  await new Promise(resolve=>setTimeout(resolve,500))
 }while(Date.now()<deadline)
 throw new Error('Gmail no confirmó el envío o no se generó la copia posterior al SMTP.')
}

const userId=randomUUID()
const finalPassword='Final!8b'+randomBytes(10).toString('hex')
try{
 const existing=sql("SELECT COUNT(*) FROM AspNetUsers WHERE NormalizedEmail='VMADRIDB@MIUMG.EDU.GT';")
 if(existing!=='0')throw new Error('Ya existe una cuenta con el correo institucional; no se modificará.')
 sql("DECLARE @id uniqueidentifier='"+userId+"'; INSERT INTO AspNetUsers (Id,Email,NormalizedEmail,FullName,Status,CreatedAtUtc,UpdatedAtUtc,AccessFailedCount,ConcurrencyStamp,EmailConfirmed,LockoutEnabled,NormalizedUserName,SecurityStamp,TwoFactorEnabled,UserName,PhoneNumberConfirmed,MustChangePassword) VALUES (@id,'"+targetEmail+"','VMADRIDB@MIUMG.EDU.GT','Verificación SMTP CUDEP',1,SYSUTCDATETIME(),SYSUTCDATETIME(),0,NEWID(),1,1,'VMADRIDB@MIUMG.EDU.GT',NEWID(),0,'"+targetEmail+"',0,0); INSERT INTO AspNetUserRoles(UserId,RoleId,AssignedAtUtc) SELECT @id,Id,SYSUTCDATETIME() FROM AspNetRoles WHERE NormalizedName='OPERADOR';")

 const started=Date.now()
 const forgot=await call('/auth/forgot-password','POST',{email:targetEmail})
 const sent=await newestMail(started)
 const token=new URL(sent.link).searchParams.get('resetToken')
 if(!token)throw new Error('El enlace SMTP no contiene token.')
 await call('/auth/reset-password','POST',{token,newPassword:finalPassword})
 const verified=(await call('/auth/login','POST',{email:targetEmail,password:finalPassword})).data
 if(verified.user.email.toLowerCase()!==targetEmail)throw new Error('El login posterior no corresponde a la cuenta de prueba.')
 console.log(JSON.stringify({
  recipient:targetEmail,
  forgotStatus:forgot.status,
  neutralMessage:forgot.data.message,
  smtpArchive:sent.path,
  resetCompleted:true,
  loginAfterReset:true
 },null,2))
}finally{
 sql("DECLARE @id uniqueidentifier='"+userId+"'; DELETE FROM Sessions WHERE UserId=@id; DELETE FROM PasswordRecoveryTokens WHERE UserId=@id; DELETE FROM PasswordCredentials WHERE UserId=@id; DELETE FROM AccessAudits WHERE UserId=@id; DELETE FROM AspNetUserRoles WHERE UserId=@id; DELETE FROM AspNetUserClaims WHERE UserId=@id; DELETE FROM AspNetUserLogins WHERE UserId=@id; DELETE FROM AspNetUserTokens WHERE UserId=@id; DELETE FROM AuditEntries WHERE UserEmail='"+targetEmail+"' OR (EntityType='User' AND EntityId='"+userId+"'); DELETE FROM AspNetUsers WHERE Id=@id;")
}
