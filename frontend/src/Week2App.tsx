// eslint-disable-next-line @typescript-eslint/ban-ts-comment -- TODO: retirar al completar el tipado heredado.
// @ts-nocheck
// TODO: tipar contratos heredados de API, props y estado antes de retirar esta supresión.
/* eslint-disable react-hooks/exhaustive-deps */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { week2Api as api } from './week2Api'
import Week3IoT from './Week3IoT'
import EnergyPanel from './EnergyPanel'
import { AlertsPanel, MaintenancePanel, NotificationCenter } from './Sprint5Operations'
import { humanizeInterfaceMessage } from './notificationMessages'
import { OperationalDashboard, TerritoryManager, TelemetryMonitor, AgronomyManager, CropPlanner } from './Modules1To7'
import { AutomationPanel, WaterSupplyPanel, ManualIrrigationPanel, WaterOperationsPanel } from './Modules7To10'
import { MasterCatalogManager, TwoFactorSetup, UserSecurityManager } from './Sprint2Administration'
import { DialogHost } from './DialogHost'

const Leaf = () => <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M20.8 3.2C14.4 3 8.2 5.7 5.2 10.5c-2.1 3.4-.8 6.8 1.3 8.1 2.3 1.4 5.5.4 7.2-2.4 1.8-2.8 2-6 6.8-11.8.4-.5.6-.8.3-1.2ZM5 21c1.2-4.7 4.2-8.4 9.2-11.2-4.1 3.6-5.7 7.1-6.2 11.2H5Z"/></svg>
const catalogLabels = { SensorType: 'Tipos de sensor', DeviceType: 'Tipos de dispositivo', MeasurementUnit: 'Unidades de medida', OperationalStatus: 'Estados operativos' }
const statusLabels = { Active: 'Activo', Blocked: 'Bloqueado', Disabled: 'Deshabilitado' }
const dataTypeOptions = [{value:'integer',label:'Número entero'},{value:'decimal',label:'Número decimal'},{value:'boolean',label:'Sí / No'},{value:'text',label:'Texto'},{value:'time',label:'Hora'}]
const parameterLabels = { SENSOR_OFFLINE_MINUTES:'Tiempo para detectar un sensor desconectado', DEFAULT_IRRIGATION_MINUTES:'Duración predeterminada del riego', MAX_LOGIN_ATTEMPTS:'Intentos máximos de inicio de sesión', TELEMETRY_INTERVAL_SECONDS:'Intervalo de envío de telemetría', AUDIT_RETENTION_DAYS:'Retención de auditoría sensible (días)' }
const emptyCatalogForm = {code:'',name:'',description:'',symbol:'',isActive:true}

function Access({ onLogin }) {
  const resetToken = useMemo(() => new URLSearchParams(window.location.search).get('resetToken') ?? '', [])
  const [mode, setMode] = useState(resetToken ? 'reset' : 'login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [code, setCode] = useState('')
  const [challenge, setChallenge] = useState(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  const goToLogin = () => {
    window.history.replaceState({}, document.title, window.location.pathname)
    setMode('login'); setPassword(''); setConfirmation(''); setCode(''); setChallenge(null); setMessage('')
  }

  const submit = async event => {
    event.preventDefault(); setBusy(true); setMessage('')
    try {
      if (mode === 'forgot') {
        const result = await api.forgot(email)
        setMessage(result.message)
        return
      }
      if (mode === 'reset') {
        if (password !== confirmation) throw new Error('Las contraseñas no coinciden.')
        await api.resetPassword(resetToken, password)
        window.history.replaceState({}, document.title, window.location.pathname)
        setMode('login'); setPassword(''); setConfirmation('')
        setMessage('Contraseña actualizada. Ya puedes iniciar sesión.')
        return
      }
      if (mode === '2fa') {
        onLogin(await api.loginTwoFactor(email, challenge.challengeToken, code))
        return
      }
      const result = await api.login(email, password)
      // Con segundo factor activo la respuesta no trae sesion: hay que canjear el desafio.
      if (result?.requiresTwoFactor) {
        setChallenge(result); setPassword(''); setCode(''); setMode('2fa')
        setMessage('Contraseña verificada. Ingresa el código de tu aplicación de autenticación.')
        return
      }
      onLogin(result)
    } catch (error) { setMessage(error.message) } finally { setBusy(false) }
  }

  const title = mode === 'login' ? 'Bienvenido de nuevo' : mode === '2fa' ? 'Verificación en dos pasos' : mode === 'forgot' ? 'Recupera tu acceso' : 'Define tu nueva contraseña'
  const subtitle = mode === 'login' ? 'Ingresa con uno de los perfiles habilitados.' : mode === '2fa' ? 'Escribe el código de seis dígitos de tu aplicación, o uno de tus códigos de recuperación.' : mode === 'forgot' ? 'Te enviaremos un enlace de un solo uso si el correo está registrado.' : 'El enlace es válido durante 30 minutos y solo puede utilizarse una vez.'

  return <main className="w2-login"><section className="w2-login-story"><div className="w2-logo"><span><Leaf/></span> Sistema de Riego</div><div><p className="w2-kicker">CUDEP · AGRICULTURA INTELIGENTE</p><h1>Seguridad para<br/>cada <em>decisión.</em></h1><p>Acceso por roles, datos maestros centralizados y trazabilidad de las operaciones del sistema.</p></div><small>Avance académico · Semana 2</small></section><section className="w2-login-form"><form onSubmit={submit}><p className="w2-kicker">ACCESO INSTITUCIONAL</p><h2>{title}</h2><p>{subtitle}</p>
    {mode !== 'reset' && mode !== '2fa' && <label>Correo electrónico<input type="email" value={email} onChange={event=>setEmail(event.target.value)} required autoComplete="email"/></label>}
    {mode === '2fa' && <label>Código de verificación<input value={code} onChange={event=>setCode(event.target.value)} required autoFocus inputMode="numeric" autoComplete="one-time-code" placeholder="000000"/></label>}
    {mode === 'login' && <label>Contraseña<input type="password" value={password} onChange={event=>setPassword(event.target.value)} required autoComplete="current-password"/></label>}
    {mode === 'reset' && <><label>Nueva contraseña<input type="password" value={password} onChange={event=>setPassword(event.target.value)} required minLength={8} autoComplete="new-password"/></label><label>Confirmar nueva contraseña<input type="password" value={confirmation} onChange={event=>setConfirmation(event.target.value)} required minLength={8} autoComplete="new-password"/></label><p className="w2-password-rules">Mínimo 8 caracteres, con mayúscula, minúscula, número y símbolo.</p></>}
    {message&&<div className="w2-alert" role="status">{message}</div>}
    <button disabled={busy}>{busy?'Procesando…':mode==='login'?'Iniciar sesión':mode==='2fa'?'Verificar código':mode==='forgot'?'Enviar enlace':'Guardar nueva contraseña'} <b>→</b></button>
    {mode==='login'&&<button type="button" className="w2-forgot-link" onClick={()=>{setMode('forgot');setMessage('')}}>¿Olvidaste tu contraseña?</button>}
    {mode!=='login'&&<button type="button" className="w2-forgot-link" onClick={goToLogin}>← Volver al inicio de sesión</button>}
  </form></section></main>
}

function RequiredPasswordChange({ session, onComplete, onLogout }) {
  const [currentPassword,setCurrentPassword]=useState(''),[newPassword,setNewPassword]=useState(''),[confirmation,setConfirmation]=useState(''),[message,setMessage]=useState(''),[busy,setBusy]=useState(false)
  const submit=async event=>{event.preventDefault();setMessage('');if(newPassword!==confirmation){setMessage('Las contraseñas no coinciden.');return}setBusy(true);try{onComplete(await api.changeRequiredPassword(session.accessToken,currentPassword,newPassword))}catch(error){setMessage(error.message)}finally{setBusy(false)}}
  return <main className="w2-login"><section className="w2-login-story"><div className="w2-logo"><span><Leaf/></span> Sistema de Riego</div><div><p className="w2-kicker">PROTECCIÓN DE CUENTA</p><h1>Una clave<br/><em>solo tuya.</em></h1><p>La contraseña temporal cumplió su propósito. Reemplázala antes de acceder a cualquier módulo.</p></div><small>Sesión restringida hasta completar el cambio</small></section><section className="w2-login-form"><form onSubmit={submit}><p className="w2-kicker">CAMBIO OBLIGATORIO</p><h2>Crea tu contraseña definitiva</h2><p>Por seguridad, vuelve a escribir la contraseña temporal y elige una nueva.</p><label>Contraseña temporal<input type="password" value={currentPassword} onChange={event=>setCurrentPassword(event.target.value)} required autoComplete="current-password"/></label><label>Nueva contraseña<input type="password" value={newPassword} onChange={event=>setNewPassword(event.target.value)} required minLength={8} autoComplete="new-password"/></label><label>Confirmar nueva contraseña<input type="password" value={confirmation} onChange={event=>setConfirmation(event.target.value)} required minLength={8} autoComplete="new-password"/></label><p className="w2-password-rules">Mínimo 8 caracteres, con mayúscula, minúscula, número y símbolo.</p>{message&&<div className="w2-alert" role="alert">{message}</div>}<button disabled={busy}>{busy?'Actualizando…':'Cambiar contraseña y continuar'} <b>→</b></button><button type="button" className="w2-forgot-link" onClick={onLogout}>Cancelar y cerrar sesión</button></form></section></main>
}

function Overview({ session, onNavigate }) {
  return <><div className="w2-page-title"><div><p className="w2-kicker">CENTRO DE CONTROL</p><h1>Buen día, {session.user.fullName.split(' ')[0]}</h1><p>Administra la seguridad y los datos que preparan la automatización del riego.</p></div><span className="w2-role">{session.user.roles.join(' · ')}</span></div><div className="w2-irrigation-strip"><div><span>💧</span><p><b>Riego</b><small>Parámetros listos</small></p></div><div><span>◉</span><p><b>Sensores</b><small>Catálogos activos</small></p></div><div><span>⌁</span><p><b>Telemetría</b><small>Frecuencia definida</small></p></div><div><span>✓</span><p><b>Seguridad</b><small>Acceso protegido</small></p></div></div><div className="w2-cards"><button className="w2-feature-card" onClick={()=>onNavigate('users')}><span className="feature-icon">♙</span><strong>03 perfiles</strong><h3>Usuarios y roles</h3><p>Administrador, Técnico y Operador con responsabilidades diferenciadas.</p></button><button className="w2-feature-card" onClick={()=>onNavigate('catalogs')}><span className="feature-icon">◉</span><strong>04 grupos</strong><h3>Catálogos de campo</h3><p>Sensores, dispositivos, unidades de medida y estados operativos.</p></button><button className="w2-feature-card" onClick={()=>onNavigate('audit')}><span className="feature-icon">✓</span><strong>100% trazable</strong><h3>Auditoría</h3><p>Los accesos y cambios importantes generan registros verificables.</p></button></div></>
}

function Users({ session, notify }) {
  const [users,setUsers]=useState([]); const [roles,setRoles]=useState([]); const load=async()=>{const [u,r]=await Promise.all([api.users(session.accessToken),api.roles(session.accessToken)]);setUsers(u);setRoles(r)}
  useEffect(()=>{load().catch(e=>notify(e.message))},[])
  const changeStatus=async(user,status)=>{await api.setUserStatus(session.accessToken,user.id,status);notify('Estado actualizado.');await load()}
  const changeRole=async(user,role)=>{await api.setUserRoles(session.accessToken,user.id,[role]);notify('Rol actualizado.');await load()}
  return <><div className="w2-page-title"><div><p className="w2-kicker">CONTROL DE ACCESO</p><h1>Usuarios y privilegios</h1><p>Administra el estado de las cuentas y sus funciones dentro del sistema.</p></div><span className="w2-count">{users.length} usuarios</span></div><div className="w2-table-card"><table><thead><tr><th>Usuario</th><th>Rol</th><th>Estado</th></tr></thead><tbody>{users.map(user=><tr key={user.id}><td><b>{user.fullName}</b><small>{user.email}</small></td><td><select value={user.roles[0]??''} onChange={e=>changeRole(user,e.target.value).catch(x=>notify(x.message))}>{roles.map(role=><option key={role.name}>{role.name}</option>)}</select></td><td><select value={user.status} onChange={e=>changeStatus(user,e.target.value).catch(x=>notify(x.message))}>{Object.entries(statusLabels).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></td></tr>)}</tbody></table></div><div className="w2-permissions">{roles.map(role=><article key={role.name}><h3>{role.name}</h3><p>{role.description}</p><div>{role.permissions.map(p=><span key={p}>{p}</span>)}</div></article>)}</div></>
}

function Catalogs({ session, notify }) {
  const [kind,setKind]=useState('SensorType'); const [items,setItems]=useState([]); const [form,setForm]=useState(emptyCatalogForm); const [editingId,setEditingId]=useState(null); const canEdit=session.user.roles.some(x=>x==='Administrador'||x==='Tecnico'); const isAdmin=session.user.roles.includes('Administrador')
  const resetForm=()=>{setForm(emptyCatalogForm);setEditingId(null)}; const load=()=>api.catalogs(session.accessToken,kind).then(setItems); useEffect(()=>{resetForm();load().catch(e=>notify(e.message))},[kind])
  const submit=async e=>{e.preventDefault();if(editingId){await api.updateCatalog(session.accessToken,kind,editingId,form);notify('Elemento actualizado correctamente.')}else{await api.createCatalog(session.accessToken,kind,form);notify('Elemento agregado correctamente.')}resetForm();await load()}
  const edit=item=>{setEditingId(item.id);setForm({code:item.code,name:item.name,description:item.description??'',symbol:item.symbol??'',isActive:item.isActive});window.scrollTo({top:0,behavior:'smooth'})}
  const toggle=async item=>{await api.updateCatalog(session.accessToken,kind,item.id,{code:item.code,name:item.name,description:item.description,symbol:item.symbol,isActive:!item.isActive});await load()}
  const remove=async item=>{await api.deleteCatalog(session.accessToken,kind,item.id);if(editingId===item.id)resetForm();notify('Elemento eliminado.');await load()}
  return <><div className="w2-page-title"><div><p className="w2-kicker">DATOS MAESTROS</p><h1>Catálogos generales</h1><p>Valores controlados que serán reutilizados por sensores y dispositivos.</p></div></div><div className="w2-tabs">{Object.entries(catalogLabels).map(([value,label])=><button className={kind===value?'active':''} onClick={()=>setKind(value)} key={value}>{label}</button>)}</div>{canEdit&&<form className={`w2-inline-form ${editingId?'editing':''}`} onSubmit={e=>submit(e).catch(x=>notify(x.message))}>{editingId&&<div className="w2-editing-note">Editando un registro existente</div>}<input placeholder="Código" value={form.code} onChange={e=>setForm({...form,code:e.target.value})} required/><input placeholder="Nombre" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} required/><input placeholder="Descripción" value={form.description} onChange={e=>setForm({...form,description:e.target.value})}/><input className="short" placeholder="Símbolo" value={form.symbol} onChange={e=>setForm({...form,symbol:e.target.value})}/><div className="w2-form-actions"><button>{editingId?'Guardar cambios':'Agregar'}</button>{editingId&&<button type="button" className="secondary" onClick={resetForm}>Cancelar</button>}</div></form>}<div className="w2-table-card"><table><thead><tr><th>Código</th><th>Nombre</th><th>Descripción</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>{items.map(item=><tr key={item.id}><td><code>{item.code}</code></td><td><b>{item.name}</b>{item.symbol&&<small>{item.symbol}</small>}</td><td>{item.description||'—'}</td><td><span className={item.isActive?'w2-status on':'w2-status'}>{item.isActive?'Activo':'Inactivo'}</span></td><td className="actions">{canEdit&&<button onClick={()=>toggle(item).catch(x=>notify(x.message))}>Cambiar estado</button>}{canEdit&&<button className="edit" onClick={()=>edit(item)}>Editar</button>}{isAdmin&&<button className="danger" onClick={()=>remove(item).catch(x=>notify(x.message))}>Eliminar</button>}</td></tr>)}</tbody></table></div></>
}

function Settings({ session, notify }) {
  const [items,setItems]=useState([]); const load=()=>api.settings(session.accessToken).then(setItems); useEffect(()=>{load().catch(e=>notify(e.message))},[])
  const save=async item=>{await api.saveSetting(session.accessToken,item.key,{value:item.value,dataType:item.dataType,category:item.category,description:item.description,isEditable:item.isEditable});notify('Parámetro actualizado.');await load()}
  const update=(id,changes)=>setItems(items.map(x=>x.id===id?{...x,...changes}:x));
  const valueControl=item=>item.dataType==='boolean'?<div className="w2-boolean-control"><button className={item.value==='true'?'active':''} onClick={()=>update(item.id,{value:'true'})}>Sí</button><button className={item.value==='false'?'active':''} onClick={()=>update(item.id,{value:'false'})}>No</button></div>:<input type={item.dataType==='integer'||item.dataType==='decimal'?'number':item.dataType==='time'?'time':'text'} step={item.dataType==='integer'?'1':item.dataType==='decimal'?'0.01':undefined} value={item.value} onChange={e=>update(item.id,{value:e.target.value})}/>;
  return <><div className="w2-page-title"><div><p className="w2-kicker">CONFIGURACIÓN DE RIEGO</p><h1>Parámetros globales</h1><p>Define frecuencias, tiempos y valores utilizados por sensores y procesos automáticos.</p></div></div><div className="w2-settings">{items.map(item=><article key={item.id}><span>{item.category}</span><h3>{item.description||parameterLabels[item.key]||item.key}</h3><code className="w2-parameter-key">{item.key}</code><div className="w2-parameter-editor"><div className="w2-field"><label>Valor configurado</label>{valueControl(item)}</div><div className="w2-field w2-select-wrap"><label>Tipo de valor</label><select className="w2-parameter-type" value={item.dataType} onChange={e=>update(item.id,{dataType:e.target.value,value:e.target.value==='boolean'?'true':item.value})}>{dataTypeOptions.map(type=><option key={type.value} value={type.value}>{type.label}</option>)}</select></div><button onClick={()=>save(item).catch(x=>notify(x.message))}>Guardar cambios</button></div></article>)}</div></>
}

function Audit({ session, notify }) {
  const empty={from:'',to:'',user:'',action:'',entity:''}
  const [items,setItems]=useState([]),[filters,setFilters]=useState(empty),[options,setOptions]=useState({users:[],actions:[],entities:[]})
  const query=()=>{const p=new URLSearchParams();Object.entries(filters).forEach(([key,value])=>value&&p.set(key,value));return p.toString()}
  const load=()=>api.audit(session.accessToken,query()).then(setItems)
  useEffect(()=>{Promise.all([load(),api.auditFilters(session.accessToken).then(setOptions)]).catch(e=>notify(e.message))},[])
  const apply=e=>{e.preventDefault();load().catch(x=>notify(x.message))}
  const clear=()=>{setFilters(empty);api.audit(session.accessToken).then(setItems).catch(e=>notify(e.message))}
  const exportFile=format=>api.exportAudit(session.accessToken,query(),format).then(()=>notify(`Auditoría ${format.toUpperCase()} descargada.`)).catch(e=>notify(e.message))
  return <><div className="w2-page-title"><div><p className="w2-kicker">TRAZABILIDAD</p><h1>Auditoría sensible</h1><p>Cambios con valores antes/después, IP y correlación de extremo a extremo.</p></div><span className="w2-count">{items.length} registros</span></div>
  <form className="s14-filters" onSubmit={apply}><label>Desde<input type="date" value={filters.from} onChange={e=>setFilters({...filters,from:e.target.value})}/></label><label>Hasta<input type="date" value={filters.to} onChange={e=>setFilters({...filters,to:e.target.value})}/></label><label>Usuario<select value={filters.user} onChange={e=>setFilters({...filters,user:e.target.value})}><option value="">Todos</option>{options.users.map(x=><option key={x}>{x}</option>)}</select></label><label>Acción<select value={filters.action} onChange={e=>setFilters({...filters,action:e.target.value})}><option value="">Todas</option>{options.actions.map(x=><option key={x}>{x}</option>)}</select></label><label>Entidad<select value={filters.entity} onChange={e=>setFilters({...filters,entity:e.target.value})}><option value="">Todas</option>{options.entities.map(x=><option key={x}>{x}</option>)}</select></label><button>Aplicar</button><button type="button" onClick={clear}>Limpiar</button><button type="button" onClick={()=>exportFile('csv')}>CSV</button><button type="button" onClick={()=>exportFile('xlsx')}>Excel</button></form>
  <div className="w2-timeline">{items.map(item=><article key={item.id}><span className="w2-dot"/><div><b>{item.actionType} · {item.entityType}</b><p>{item.detail}{item.entityId? ` #${item.entityId}`:''}</p><small>{item.userEmail??item.origin} · {new Date(item.occurredAtUtc).toLocaleString('es-GT')} · IP {item.ipAddress??'—'}</small>{(item.beforeJson||item.afterJson)&&<details><summary>Valores modificados</summary><pre>Antes: {item.beforeJson??'—'}{String.fromCharCode(10)}Después: {item.afterJson??'—'}</pre><small>CorrelationId: {item.correlationId}</small></details>}</div></article>)}</div></>
}

function Shell({ session, onLogout }) {
  const isAdmin=session.user.roles.includes('Administrador'); const canCatalog=session.user.roles.some(x=>['Administrador','Tecnico','Operador'].includes(x)); const [view,setView]=useState('inicio'); const [notificationsOpen,setNotificationsOpen]=useState(false); const [interfaceToast,setInterfaceToast]=useState(null); const notify=useCallback(message=>setInterfaceToast(humanizeInterfaceMessage(String(message))),[]); useEffect(()=>{sessionStorage.removeItem('riego.localNotices')},[]); useEffect(()=>{if(!interfaceToast)return;const timer=window.setTimeout(()=>setInterfaceToast(null),5000);return()=>window.clearTimeout(timer)},[interfaceToast])
  // Agrupado segun los modulos de la planificacion: diecinueve entradas planas
  // hacian que pantallas como Unidades de medida o Inventario IoT no se
  // encontraran. Inicio queda suelto por ser la portada.
  const nav=useMemo(()=>[
    {group:null,items:[{id:'inicio',label:'Inicio',icon:'⌂'}]},
    {group:'Datos maestros',items:[{id:'territory',label:'Finca',icon:'⌖'},...(canCatalog?[{id:'catalogs',label:'Catálogos',icon:'◉'}]:[])]},
    {group:'Infraestructura IoT',items:[...(canCatalog?[{id:'iot',label:'Red IoT',icon:'⌁'}]:[]),{id:'telemetry',label:'Lecturas',icon:'≈'}]},
    {group:'Agronomía y cultivos',items:[{id:'agronomy',label:'Agronomía',icon:'♧'},{id:'planning',label:'Cultivos',icon:'◷'}]},
    {group:'Operación de riego',items:[{id:'automation',label:'Automatización',icon:'↻'},{id:'manual',label:'Riego manual',icon:'☂'},{id:'supply',label:'Bomba y tanque',icon:'◒'},{id:'energy',label:'Energía solar',icon:'☀'},{id:'operations',label:'Consumo',icon:'▥'}]},
    {group:'Alertas y mantenimiento',items:[{id:'alerts',label:'Alertas',icon:'♢'},{id:'maintenance',label:'Mantenimiento',icon:'⚒'}]},
    ...(isAdmin?[{group:'Seguridad',items:[{id:'users',label:'Usuarios',icon:'♙'},{id:'security',label:'Seguridad 2FA',icon:'◆'}]}]:[]),
    {group:'Configuración y auditoría',items:[{id:'overview',label:'Administración',icon:'▦'},...(isAdmin?[{id:'settings',label:'Parámetros',icon:'⚙'},{id:'audit',label:'Auditoría',icon:'✓'}]:[])]},
  ].filter(x=>x.items.length>0),[isAdmin,canCatalog])
  return <main className="w2-app"><aside><div className="w2-logo"><span><Leaf/></span><div>Sistema de Riego<small>Agua inteligente</small></div></div><nav>{nav.map((section,index)=><div className="w2-nav-group" key={section.group??`inicio-${index}`}>{section.group&&<p className="w2-nav-group-title">{section.group}</p>}{section.items.map(item=><button key={item.id} className={view===item.id?'active':''} onClick={()=>setView(item.id)}><span className="w2-nav-icon">{item.icon}</span>{item.label}</button>)}</div>)}</nav><div className="w2-profile"><span>{session.user.fullName.slice(0,2).toUpperCase()}</span><div><b>{session.user.fullName}</b><small>{session.user.roles.join(', ')}</small></div></div><button className="w2-logout" onClick={onLogout}>Cerrar sesión</button></aside><NotificationCenter session={session} onOpenChange={setNotificationsOpen} onNavigate={setView}/>{interfaceToast&&<div className="n-interface-toast" role="status" aria-live="polite"><span>{interfaceToast}</span><button aria-label="Cerrar mensaje" onClick={()=>setInterfaceToast(null)}>×</button></div>}<section className={`w2-content${notificationsOpen ? ' notification-drawer-open' : ''}`}>{view==='inicio'&&<OperationalDashboard session={session} onNavigate={setView} notify={notify}/>} {view==='territory'&&<TerritoryManager session={session} notify={notify}/>} {view==='telemetry'&&<TelemetryMonitor session={session} notify={notify}/>} {view==='agronomy'&&<AgronomyManager session={session} notify={notify}/>} {view==='planning'&&<CropPlanner session={session} notify={notify}/>} {view==='automation'&&<AutomationPanel session={session} notify={notify}/>} {view==='supply'&&<WaterSupplyPanel session={session} notify={notify}/>} {view==='energy'&&<EnergyPanel session={session} notify={notify}/>} {view==='manual'&&<ManualIrrigationPanel session={session} notify={notify}/>} {view==='operations'&&<WaterOperationsPanel session={session} notify={notify}/>} {view==='alerts'&&<AlertsPanel session={session} notify={notify}/>} {view==='maintenance'&&<MaintenancePanel session={session} notify={notify}/>} {view==='overview'&&<Overview session={session} onNavigate={setView}/>} {view==='users'&&<UserSecurityManager session={session} notify={notify}/>} {view==='security'&&<TwoFactorSetup session={session} notify={notify}/>} {view==='catalogs'&&<MasterCatalogManager session={session} notify={notify}/>} {view==='iot'&&<Week3IoT session={session} notify={notify}/>} {view==='settings'&&<Settings session={session} notify={notify}/>} {view==='audit'&&<Audit session={session} notify={notify}/>}</section></main>
}

export default function Week2App(){
  const [session,setSession]=useState(()=>{try{return JSON.parse(sessionStorage.getItem('riego.session'))}catch{return null}})
  const persist=useCallback(data=>{const safe={...data,refreshToken:undefined};sessionStorage.setItem('riego.session',JSON.stringify(safe));setSession(safe)},[])
  const logout=useCallback(async()=>{try{if(session)await api.logout(session)}finally{sessionStorage.removeItem('riego.session');setSession(null)}},[session])
  useEffect(()=>{if(!session?.accessTokenExpiresAtUtc)return;const expires=new Date(session.accessTokenExpiresAtUtc).getTime();const delay=Math.max(0,expires-Date.now()-60000);const timer=window.setTimeout(()=>api.refresh(session.refreshToken).then(persist).catch(()=>logout()),delay);return()=>window.clearTimeout(timer)},[session?.accessTokenExpiresAtUtc,session?.refreshToken,persist,logout])
  return <>{(()=>{const tree=session?.user?.mustChangePassword?<RequiredPasswordChange session={session} onComplete={persist} onLogout={logout}/>:session?<Shell session={session} onLogout={logout}/>:<Access onLogin={persist}/>;return tree})()}<DialogHost/></>
}
