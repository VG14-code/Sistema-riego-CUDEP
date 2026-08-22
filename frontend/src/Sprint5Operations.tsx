import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import './sprint5.css'

interface Session { accessToken: string; user: { fullName: string; roles: string[] } }
interface Props { session: Session; notify: (message: string) => void }
interface AlertItem { id: number; type: string; severity: string; status: string; origin: string; description: string; relatedEntityType?: string; relatedEntityId?: string; relatedEntityName?: string; raisedAtUtc: string; escalationLevel: number }
interface AlertGroup { key: string; items: AlertItem[] }
type RealtimeStatus = 'connecting' | 'online' | 'reconnecting' | 'offline'
interface Plan { id: string; name: string; frequency: string; intervalDays?: number; equipmentType: string; equipmentId: string; scheduledAtUtc: string; status: string; assignedToEmail?: string; notes?: string }
interface Incident { id: number; title: string; description: string; equipmentType: string; equipmentId: string; equipmentName?: string; severity: string; status: string; origin: string; createdAtUtc: string; assignedToEmail?: string; notes?: string }
interface Activity { id: number; title: string; equipmentType: string; equipmentId: string; status: string; scheduledAtUtc: string; performedAtUtc?: string }
const root = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`
const hubRoot = root.replace(/\/api$/, '')
async function api<T>(path: string, token: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`${root}${path}`, { method, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` }, body: body === undefined ? undefined : JSON.stringify(body) })
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error((data as { message?: string } | null)?.message ?? `Solicitud rechazada (${response.status}).`)
  return data as T
}
const when = (value: string) => new Date(value).toLocaleString('es-GT')

function Message({ description, name, type, id, heading = false }: { description: string; name?: string; type?: string; id?: string; heading?: boolean }) {
  const marker = '\nDetalle técnico: '
  const parts = description.split(marker)
  const rawTimeout = /^Comando\s+(.+?)\s+[0-9a-f-]{36}\s+expiró sin ACK\.$/i.test(description)
  const subject = name ? `${type ?? 'Equipo'} ${name}` : type ?? 'El equipo'
  const message = rawTimeout ? `${subject} no respondió dentro del tiempo esperado.` : parts[0]
  const technical = parts[1] ?? (rawTimeout ? description : undefined)
  const hasIdentifier = Boolean(id && /^[0-9a-f-]{36}$/i.test(id))
  return <>{heading ? <h3>{message}</h3> : <span>{message}</span>}{(technical || hasIdentifier) && <details className="s5-technical"><summary>Ver detalle técnico</summary>{technical && <code>{technical}</code>}{hasIdentifier && <small>ID: {id}</small>}</details>}</>
}

function groupAlerts(items: AlertItem[]): AlertGroup[] {
  const groups = new Map<string, AlertItem[]>()
  items.forEach(item => {
    const humanMessage = item.description.split('\nDetalle técnico: ')[0].trim().toLocaleLowerCase('es')
    const entity = item.relatedEntityId ?? item.relatedEntityName ?? item.origin
    const key = `${item.type}|${entity}|${humanMessage}`
    groups.set(key, [...(groups.get(key) ?? []), item])
  })
  return Array.from(groups, ([key, occurrences]) => ({
    key,
    items: occurrences.sort((left, right) => new Date(right.raisedAtUtc).getTime() - new Date(left.raisedAtUtc).getTime()),
  })).sort((left, right) => new Date(right.items[0].raisedAtUtc).getTime() - new Date(left.items[0].raisedAtUtc).getTime())
}

function useAlerts(session: Session, notify: (message: string) => void, onRaised?: (alert: AlertItem) => void, onConnectionState?: (status: RealtimeStatus) => void) {
  const [items, setItems] = useState<AlertItem[]>([])
  const onRaisedRef = useRef(onRaised)
  useEffect(() => { onRaisedRef.current = onRaised }, [onRaised])
  const load = useCallback(() => api<AlertItem[]>('/alerts', session.accessToken).then(setItems).catch((error: Error) => notify(error.message)), [session.accessToken, notify])
  useEffect(() => {
    load()
    const connection = new signalR.HubConnectionBuilder().withUrl(`${hubRoot}/hubs/telemetry`, { accessTokenFactory: () => session.accessToken }).withAutomaticReconnect().configureLogging(signalR.LogLevel.None).build()
    connection.on('alertRaised', (alert: AlertItem) => { onRaisedRef.current?.(alert); void load() })
    onConnectionState?.('connecting')
    connection.onreconnecting(() => onConnectionState?.('reconnecting'))
    connection.onreconnected(() => onConnectionState?.('online'))
    connection.onclose(() => onConnectionState?.('offline'))
    connection.start().then(() => onConnectionState?.('online')).catch(() => onConnectionState?.('offline'))
    return () => { void connection.stop() }
  }, [load, onConnectionState, session.accessToken])
  return { items, load }
}

export function NotificationCenter({ session }: { session: Session }) {
  const [open, setOpen] = useState(false)
  const [toast, setToast] = useState<AlertItem | null>(null)
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>('connecting')
  const quiet = useCallback(() => undefined, [])
  const handleRaised = useCallback((alert: AlertItem) => setToast(alert), [])
  const handleConnectionState = useCallback((status: RealtimeStatus) => setRealtimeStatus(status), [])
  const { items, load } = useAlerts(session, quiet, handleRaised, handleConnectionState)
  const active = useMemo(() => items.filter(item => item.status === 'Activa'), [items])
  const groups = useMemo(() => groupAlerts(active), [active])
  const summary = useMemo(() => {
    const count = (severity: string) => active.filter(item => item.severity === severity).length
    const parts = [[count('Crítica'), 'crítica', 'críticas'], [count('Advertencia'), 'advertencia', 'advertencias'], [count('Informativa'), 'informativa', 'informativas']] as const
    return parts.filter(([total]) => total > 0).map(([total, singular, plural]) => `${total} ${total === 1 ? singular : plural}`).join(' · ')
  }, [active])
  useEffect(() => {
    if (!toast) return
    const timer = window.setTimeout(() => setToast(null), 7000)
    return () => window.clearTimeout(timer)
  }, [toast])
  const acknowledge = async (alerts: AlertItem[]) => {
    await Promise.all(alerts.map(item => api(`/alerts/${item.id}/acknowledge`, session.accessToken, 'POST')))
    await load()
  }
  const realtimeLabel = realtimeStatus === 'online' ? 'Tiempo real activo' : realtimeStatus === 'reconnecting' ? 'Reconectando en tiempo real' : realtimeStatus === 'connecting' ? 'Conectando en tiempo real' : 'Tiempo real no disponible'
  return <div className={`n-center${open ? ' open' : ''}`}>
    <span className={`n-realtime ${realtimeStatus}`} role="status" title={realtimeStatus === 'reconnecting' ? 'Se perdió temporalmente la conexión en tiempo real. Reintentando automáticamente.' : realtimeLabel}><i/>{realtimeLabel}</span>
    <button className="n-bell" onClick={() => setOpen(!open)} aria-label="Centro de notificaciones">♢{active.length > 0 && <b>{active.length}</b>}</button>
    {toast && <section className={`n-toast ${toast.severity.toLocaleLowerCase('es')}`} role="status" aria-live="assertive"><header><b>Nueva alerta {toast.severity.toLocaleLowerCase('es')}</b><button aria-label="Descartar notificación" onClick={() => setToast(null)}>×</button></header><Message description={toast.description} name={toast.relatedEntityName} type={toast.relatedEntityType} id={toast.relatedEntityId}/><small>{when(toast.raisedAtUtc)}</small><button className="n-toast-detail" onClick={() => { setOpen(true); setToast(null) }}>Ver detalle</button></section>}
    {open && <aside><header><div><small>CENTRO DE NOTIFICACIONES</small><h2>Alertas activas</h2></div><button onClick={() => setOpen(false)}>×</button></header>
      {active.length > 0 && <section className="n-summary" aria-label="Resumen de alertas activas"><b>{summary}</b><small>{groups.length} {groups.length === 1 ? 'condición activa' : 'condiciones activas'} agrupadas</small></section>}
      <div className="n-mini-list">
       {groups.slice(0, 8).map(group => {
        const latest = group.items[0]
        return <article key={group.key} className={latest.severity.toLocaleLowerCase('es')}><div className="n-group-heading"><b>{latest.relatedEntityName ?? latest.type}</b>{group.items.length > 1 && <em>{group.items.length} intentos</em>}</div><Message description={latest.description} name={latest.relatedEntityName} type={latest.relatedEntityType} id={latest.relatedEntityId}/><small>Última ocurrencia: {when(latest.raisedAtUtc)}</small>{group.items.length > 1 && <details className="n-occurrences"><summary>Ver los {group.items.length} intentos fallidos</summary>{group.items.map((item, index) => <div key={item.id}><b>Intento {group.items.length - index}</b><time>{when(item.raisedAtUtc)}</time><Message description={item.description} name={item.relatedEntityName} type={item.relatedEntityType} id={item.relatedEntityId}/></div>)}</details>}<button onClick={() => acknowledge(group.items)}>Reconocer {group.items.length > 1 ? 'todas' : ''}</button></article>
      })}{active.length === 0 && <p>Sin alertas activas.</p>}</div>
    </aside>}
  </div>
}

export function AlertsPanel({ session, notify }: Props) {
  const { items, load } = useAlerts(session, notify); const [severity, setSeverity] = useState(''); const [status, setStatus] = useState('')
  const shown = useMemo(() => items.filter(item => (!severity || item.severity === severity) && (!status || item.status === status)), [items, severity, status])
  const action = async (item: AlertItem, resolve = false) => { await api(`/alerts/${item.id}/${resolve ? 'resolve' : 'acknowledge'}`, session.accessToken, 'POST'); notify(resolve ? 'Alerta resuelta.' : 'Alerta reconocida.'); await load() }
  return <div className="s5-page"><header className="s5-hero"><div><p>MÓDULO 15 · NOTIFICACIONES</p><h1>Centro de alertas</h1><span>Condiciones detectadas por el sistema, reconocimiento, resolución y escalamiento.</span></div><div><select value={severity} onChange={e => setSeverity(e.target.value)}><option value="">Todas las severidades</option><option>Informativa</option><option>Advertencia</option><option>Crítica</option></select><select value={status} onChange={e => setStatus(e.target.value)}><option value="">Todos los estados</option><option>Activa</option><option>Reconocida</option><option>Resuelta</option></select></div></header><section className="s5-alerts">{shown.map(item => <article key={item.id} className={item.severity.toLowerCase()}><i/><div><small>{item.type} · {item.relatedEntityName ?? item.relatedEntityType ?? 'Sistema'}</small><Message heading description={item.description} name={item.relatedEntityName} type={item.relatedEntityType} id={item.relatedEntityId}/><p>{item.origin} · nivel de escalamiento {item.escalationLevel}</p><time>{when(item.raisedAtUtc)}</time></div><span className="s5-status">{item.status}</span>{item.status === 'Activa' && <button onClick={() => action(item)}>Reconocer</button>}{item.status !== 'Resuelta' && <button className="ghost" onClick={() => action(item, true)}>Resolver</button>}</article>)}</section></div>
}

export function MaintenancePanel({ session, notify }: Props) {
  const [plans, setPlans] = useState<Plan[]>([]), [incidents, setIncidents] = useState<Incident[]>([]), [activities, setActivities] = useState<Activity[]>([])
  const [plan, setPlan] = useState({ name: '', frequency: 'Recurrente', intervalDays: 30, equipmentType: 'Bomba', equipmentId: '', scheduledAtUtc: new Date(Date.now() + 86400000).toISOString().slice(0, 16), assignedToEmail: '', notes: '' })
  const load = useCallback(() => Promise.all([api<Plan[]>('/maintenance/plans', session.accessToken), api<Incident[]>('/maintenance/incidents', session.accessToken), api<Activity[]>('/maintenance/activities', session.accessToken)]).then(([p, i, a]) => { setPlans(p); setIncidents(i); setActivities(a) }).catch((error: Error) => notify(error.message)), [session.accessToken, notify])
  useEffect(() => { load() }, [load])
  const createPlan = async (event: React.FormEvent) => { event.preventDefault(); await api('/maintenance/plans', session.accessToken, 'POST', { ...plan, scheduledAtUtc: new Date(plan.scheduledAtUtc).toISOString(), assignedToUserId: null, status: 'Pendiente' }); notify('Plan de mantenimiento creado.'); setPlan({ ...plan, name: '', equipmentId: '' }); await load() }
  const removePlan = async (id: string) => { await api(`/maintenance/plans/${id}`, session.accessToken, 'DELETE'); notify('Plan eliminado.'); await load() }
  const incidentStatus = async (item: Incident, next: string) => { await api(`/maintenance/incidents/${item.id}`, session.accessToken, 'PUT', { status: next, assignedToUserId: null, assignedToEmail: item.assignedToEmail, notes: item.notes }); notify('Incidencia actualizada.'); await load() }
  return <div className="s5-page"><header className="s5-hero"><div><p>MÓDULO 14 · MANTENIMIENTO</p><h1>Gestión del ciclo operativo</h1><span>Planes preventivos, actividades e incidencias manuales o generadas desde alertas.</span></div></header><section className="m-layout"><form className="m-form" onSubmit={createPlan}><small>NUEVO PLAN</small><h2>Programar mantenimiento</h2><label>Nombre<input value={plan.name} onChange={e => setPlan({ ...plan, name: e.target.value })} required/></label><div><label>Equipo<select value={plan.equipmentType} onChange={e => setPlan({ ...plan, equipmentType: e.target.value })}><option>Bomba</option><option>Dispositivo IoT</option><option>Sensor</option><option>Válvula</option><option>Panel solar</option></select></label><label>ID del equipo<input value={plan.equipmentId} onChange={e => setPlan({ ...plan, equipmentId: e.target.value })} required/></label></div><div><label>Frecuencia<select value={plan.frequency} onChange={e => setPlan({ ...plan, frequency: e.target.value })}><option>Recurrente</option><option>Único</option></select></label><label>Intervalo (días)<input type="number" min="1" value={plan.intervalDays} onChange={e => setPlan({ ...plan, intervalDays: Number(e.target.value) })}/></label></div><label>Fecha programada<input type="datetime-local" value={plan.scheduledAtUtc} onChange={e => setPlan({ ...plan, scheduledAtUtc: e.target.value })}/></label><label>Responsable<input value={plan.assignedToEmail} onChange={e => setPlan({ ...plan, assignedToEmail: e.target.value })}/></label><label>Notas<textarea value={plan.notes} onChange={e => setPlan({ ...plan, notes: e.target.value })}/></label><button>Crear plan</button></form><section className="m-incidents"><header><small>INCIDENCIAS</small><h2>Trabajo requerido</h2></header>{incidents.map(item => <article key={item.id} className={item.origin.startsWith('Automática') ? 'automatic' : ''}><div><small>{item.origin} · {item.severity}</small><h3>{item.title}</h3><Message description={item.description} name={item.equipmentName} type={item.equipmentType} id={item.equipmentId}/><span>{item.equipmentType} · {item.equipmentName ?? 'Equipo sin nombre'}</span></div><select value={item.status} onChange={e => incidentStatus(item, e.target.value)}><option>Pendiente</option><option>En progreso</option><option>Resuelta</option></select></article>)}</section></section><section className="m-grid"><article><h2>Planes</h2>{plans.map(item => <div key={item.id}><span><b>{item.name}</b><small>{item.equipmentType} · {when(item.scheduledAtUtc)}</small></span><em>{item.status}</em><button onClick={() => removePlan(item.id)}>Eliminar</button></div>)}</article><article><h2>Historial de actividades</h2>{activities.map(item => <div key={item.id}><span><b>{item.title}</b><small>{item.equipmentType} · {when(item.scheduledAtUtc)}</small></span><em>{item.status}</em></div>)}{activities.length === 0 && <p>Sin actividades registradas.</p>}</article></section></div>
}
