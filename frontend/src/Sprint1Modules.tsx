import { useCallback, useEffect, useMemo, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { CircleMarker, MapContainer, Polygon, Popup, TileLayer } from 'react-leaflet'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import 'leaflet/dist/leaflet.css'
import { modulesApi as api, type ActivityItem, type Center, type DashboardData, type IoTDevice, type IoTNode, type IoTSensor, type Quality, type Reading, type TelemetryAggregate, type Zone } from './modulesApi'
import './sprint1.css'

interface Session { accessToken: string; user: { fullName: string } }
interface SharedProps { session: Session; notify: (message: string) => void }
interface DashboardProps extends SharedProps { onNavigate: (module: string) => void }
type ConnectionStatus = 'Conectando' | 'Online' | 'Reconectando' | 'Offline'

const hubUrl = (import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`).replace(/\/api\/?$/, '') + '/hubs/telemetry'
const fmt = (value: number | null | undefined) => Number(value ?? 0).toLocaleString('es-GT', { maximumFractionDigits: 1 })
const when = (value: string | null) => value ? new Date(value).toLocaleString('es-GT', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit', second: '2-digit' }) : 'Sin comunicación'
const simulated = (reading: Reading) => reading.transport.includes('SIMUL') || reading.transport.includes('MUESTRA')
const sectorName = (name: string) => /^sector\b/i.test(name.trim()) ? name.trim() : `Sector ${name.trim()}`

function SectionHead({ kicker, title, copy, action }: { kicker: string; title: string; copy: string; action?: React.ReactNode }) {
  return <div className="m-head"><div><p>{kicker}</p><h1>{title}</h1><span>{copy}</span></div>{action}</div>
}

function StatusPill({ status }: { status: ConnectionStatus }) {
  return <span className={`s1-status ${status.toLowerCase()}`}><i /> {status}</span>
}

function polygonPositions(value: string | null): Array<[number, number]> {
  if (!value) return []
  try {
    const parsed = JSON.parse(value) as { type?: string; coordinates?: number[][][] }
    if (parsed.type !== 'Polygon' || !parsed.coordinates?.[0]) return []
    return parsed.coordinates[0].map(([longitude, latitude]) => [latitude, longitude])
  } catch { return [] }
}

function FarmMap({ hierarchy }: { hierarchy: Center[] }) {
  const mapData = useMemo(() => {
    const zones: Array<{ zone: Zone; center: string; farm: string; sector: string; position: [number, number]; polygon: Array<[number, number]> }> = []
    const sectors: Array<{ id: string; name: string; polygon: Array<[number, number]> }> = []
    hierarchy.forEach((center, centerIndex) => center.farms.forEach((farm, farmIndex) => farm.blocks.forEach(block => block.sectors.forEach((sector, sectorIndex) => {
      sectors.push({ id: sector.id, name: sector.name, polygon: polygonPositions(sector.boundaryGeoJson) })
      sector.zones.forEach((zone, zoneIndex) => {
        const baseLat = Number(zone.latitude ?? farm.latitude ?? 16.91916)
        const baseLng = Number(zone.longitude ?? farm.longitude ?? -89.88578)
        zones.push({ zone, center: center.name, farm: farm.name, sector: sector.name, position: [baseLat + (centerIndex + sectorIndex) * .00018, baseLng + (farmIndex + zoneIndex) * .00018], polygon: polygonPositions(zone.boundaryGeoJson) })
      })
    }))))
    return { zones, sectors }
  }, [hierarchy])
  const center: [number, number] = mapData.zones[0]?.polygon[0] ?? mapData.zones[0]?.position ?? [16.91916, -89.88578]
  return <div className="s1-map"><MapContainer center={center} zoom={17} scrollWheelZoom className="s1-map-canvas">
    <TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
    {mapData.sectors.filter(item => item.polygon.length > 2).map(item => <Polygon key={`sector-${item.id}`} positions={item.polygon} pathOptions={{ color: '#426e87', weight: 2, dashArray: '7 5', fillColor: '#76a9c2', fillOpacity: .1 }}><Popup><strong>{sectorName(item.name)}</strong></Popup></Polygon>)}
    {mapData.zones.map(({ zone, center: centerName, farm, sector, position, polygon }) => {
      const online = /activo|online|disponible/i.test(zone.status)
      const popup = <Popup><strong>{zone.name}</strong><br />{centerName} → {farm} → {sector}<br />Estado: {zone.status}<br />Sensores: {zone.sensors?.map(item => item.name).join(', ') || zone.sensor || 'Sin asignar'}<br />Válvulas: {zone.valves?.map(item => item.name).join(', ') || 'Sin asignar'}</Popup>
      return polygon.length > 2
        ? <Polygon key={zone.id} positions={polygon} pathOptions={{ color: online ? '#0f9f72' : '#dc5c5c', fillColor: online ? '#28c995' : '#f47c7c', fillOpacity: .38 }}>{popup}</Polygon>
        : <CircleMarker key={zone.id} center={position} radius={11} pathOptions={{ color: online ? '#0f9f72' : '#dc5c5c', fillColor: online ? '#28c995' : '#f47c7c', fillOpacity: .82 }}>{popup}</CircleMarker>
    })}
  </MapContainer></div>
}

export function OperationalDashboard({ session, onNavigate, notify }: DashboardProps) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [hierarchy, setHierarchy] = useState<Center[]>([])
  const [nodes, setNodes] = useState<IoTNode[]>([])
  const [devices, setDevices] = useState<IoTDevice[]>([])
  const [connection, setConnection] = useState<ConnectionStatus>('Conectando')
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const load = useCallback(async () => {
    setLoading(true); setLoadError('')
    try {
      const [dashboard, tree, nodeRows, deviceRows] = await Promise.all([api.dashboard(session.accessToken), api.hierarchy(session.accessToken), api.nodes(session.accessToken), api.devices(session.accessToken)])
      setData(dashboard); setHierarchy(tree); setNodes(nodeRows); setDevices(deviceRows)
    } catch (error) {
      const message = error instanceof Error ? error.message : 'No fue posible cargar el dashboard.'
      setLoadError(message); throw error
    } finally { setLoading(false) }
  }, [session.accessToken])

  useEffect(() => { load().catch(error => notify(error instanceof Error ? error.message : 'No fue posible cargar el dashboard.')) }, [load, notify])
  useEffect(() => {
    const hub = new HubConnectionBuilder().withUrl(hubUrl, { accessTokenFactory: () => session.accessToken }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    hub.onreconnecting(() => setConnection('Reconectando')); hub.onreconnected(() => setConnection('Online')); hub.onclose(() => setConnection('Offline'))
    hub.on('telemetryReadingReceived', (reading: Reading) => {
      setData(current => current ? {
        ...current,
        generatedAtUtc: new Date().toISOString(),
        latestReadings: [reading, ...current.latestReadings.filter(item => item.id !== reading.id)].slice(0, 12),
        recentActivity: [{ type: 'TELEMETRIA', detail: `${reading.sensorName} · ${reading.value} ${reading.unitSymbol ?? ''}`, occurredAtUtc: new Date().toISOString() }, ...current.recentActivity].slice(0, 12),
      } : current)
      api.nodes(session.accessToken).then(setNodes).catch(() => undefined)
      api.devices(session.accessToken).then(setDevices).catch(() => undefined)
    })
    hub.start().then(() => setConnection('Online')).catch(() => setConnection('Offline'))
    return () => { if (hub.state !== HubConnectionState.Disconnected) void hub.stop() }
  }, [session.accessToken])

  if (loading && !data) return <div className="m-loading"><i /><span>Sincronizando infraestructura IoT…</span></div>
  if (!data) return <div className="m-loading m-load-error"><strong>No fue posible sincronizar la infraestructura IoT.</strong><span>{loadError}</span><button type="button" onClick={() => load().catch(() => undefined)}>Reintentar</button></div>
  const onlineNodes = nodes.filter(node => /online|activo/i.test(node.operationalStatus)).length
  const onlineDevices = devices.filter(device => /online|activo/i.test(device.operationalStatus)).length
  return <>
    <SectionHead kicker="MÓDULO 1 · INICIO OPERATIVO" title={`Buen día, ${session.user.fullName.split(' ')[0]}`} copy="Cartografía, telemetría multi-nodo y trazabilidad en una sola vista." action={<div className="s1-head-actions"><StatusPill status={connection} /><button onClick={() => load().catch(() => undefined)}>Actualizar</button></div>} />
    <div className="m-kpis">{[
      ['💧', `${fmt(data.averageMoisture)}%`, 'Humedad media'], ['◉', `${onlineNodes}/${nodes.length}`, 'Nodos online'], ['⌁', `${onlineDevices}/${devices.length}`, 'Dispositivos online'], ['▦', data.activeZones, 'Zonas de riego'],
      ['🚿', data.zonesIrrigating, 'Zonas regando'], ['▰', data.tankLevelPercent == null ? 'Sin datos' : `${fmt(data.tankLevelPercent)}%`, 'Nivel de tanque'], ['⚙', data.pumpStatus, 'Estado de bomba'],
      ['☀', data.batteryPercent == null ? 'Sin datos' : `${fmt(data.batteryPercent)}%`, 'Batería solar'], ['≈', `${fmt(data.todayConsumptionLiters)} L`, 'Consumo de hoy'], ['⚠', data.activeAlerts, 'Alertas activas'], ['⌁', data.invalidReadings, 'Lecturas a revisar'],
    ].map(item => <article key={item[2]}><b>{item[0]}</b><div><strong>{item[1]}</strong><span>{item[2]}</span></div></article>)}</div>
    <div className="s1-dashboard-grid"><section className="m-panel"><header><div><small>MAPA PRODUCTIVO</small><h2>Centro → finca → bloque → sector → zona</h2></div></header><FarmMap hierarchy={hierarchy} /></section>
      <section className="m-panel"><header><div><small>RED IOT</small><h2>Última comunicación</h2></div><button onClick={() => onNavigate('iot')}>Infraestructura →</button></header><div className="s1-node-list">{nodes.map(node => <article key={node.id}><i className={/online|activo/i.test(node.operationalStatus) ? 'online' : 'offline'} /><div><strong>{node.name}</strong><span>{node.code} · {node.deviceCount} dispositivos</span></div><time>{when(node.lastCommunicationUtc)}</time></article>)}</div></section></div>
    <section className="m-panel s1-readings"><header><div><small>TELEMETRÍA EN VIVO</small><h2>Múltiples zonas simultáneas</h2></div><button onClick={() => onNavigate('telemetry')}>Ver histórico →</button></header><div>{data.latestReadings.slice(0, 8).map(reading => <article key={reading.id}><div><strong>{reading.zoneName ?? 'Red general'}</strong><span>{reading.sensorName}</span></div><b>{fmt(reading.value)} {reading.unitSymbol}</b><time>{when(reading.capturedAtUtc)}</time>{simulated(reading) && <em>Simulado</em>}</article>)}</div></section>
    <section className="m-panel m-activity"><header><div><small>ACTIVIDAD CONSOLIDADA</small><h2>Telemetría, riegos y comandos</h2></div></header>{data.recentActivity.map((item: ActivityItem, index) => <div className="m-event" key={`${item.occurredAtUtc}-${index}`}><i /><div><b>{item.type.replaceAll('_', ' ')}</b><span>{item.detail}</span></div><time>{when(item.occurredAtUtc)}</time></div>)}</section>
  </>
}

export function TelemetryMonitor({ session, notify }: SharedProps) {
  const [items, setItems] = useState<Reading[]>([])
  const [quality, setQuality] = useState<Quality | null>(null)
  const [sensors, setSensors] = useState<IoTSensor[]>([])
  const [selectedSensor, setSelectedSensor] = useState('all')
  const [page, setPage] = useState(1)
  const [pageInfo, setPageInfo] = useState({ total: 0, totalPages: 1 })
  const [aggregates, setAggregates] = useState<TelemetryAggregate[]>([])
  const [connection, setConnection] = useState<ConnectionStatus>('Conectando')
  const load = useCallback(async () => {
    const [history, aggregateRows, qualityData, sensorRows] = await Promise.all([
      api.telemetryPage(session.accessToken, page, selectedSensor), api.telemetryAggregates(session.accessToken, selectedSensor),
      api.quality(session.accessToken), api.sensors(session.accessToken),
    ])
    setItems(history.items); setPageInfo({ total: history.total, totalPages: history.totalPages }); setAggregates(aggregateRows); setQuality(qualityData); setSensors(sensorRows)
  }, [session.accessToken, page, selectedSensor])
  useEffect(() => { load().catch(error => notify(error instanceof Error ? error.message : 'No fue posible cargar telemetría.')) }, [load, notify])
  useEffect(() => {
    const hub = new HubConnectionBuilder().withUrl(hubUrl, { accessTokenFactory: () => session.accessToken }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    hub.onreconnecting(() => setConnection('Reconectando')); hub.onreconnected(() => setConnection('Online')); hub.onclose(() => setConnection('Offline'))
    hub.on('telemetryReadingReceived', (reading: Reading) => { if (page === 1 && (selectedSensor === 'all' || selectedSensor === reading.sensorId)) setItems(current => [reading, ...current.filter(item => item.id !== reading.id)].slice(0, 30)) })
    hub.start().then(() => setConnection('Online')).catch(() => setConnection('Offline'))
    return () => { if (hub.state !== HubConnectionState.Disconnected) void hub.stop() }
  }, [session.accessToken, page, selectedSensor])
  const chart = useMemo(() => items.slice(0, 120).reverse().map(item => ({ time: new Date(item.capturedAtUtc).toLocaleString('es-GT', { day: '2-digit', hour: '2-digit', minute: '2-digit' }), value: Number(item.value), sensor: item.sensorName })), [items])
  return <>
    <SectionHead kicker="MÓDULO 5 · SENSORES Y LECTURAS" title="Telemetría histórica y en vivo" copy="Series por sensor, calidad, mensajes atrasados y origen transparente." action={<StatusPill status={connection} />} />
    {quality && <div className="m-quality"><article><strong>{quality.validPercent}%</strong><span>Datos válidos</span></article><article><strong>{quality.availabilityPercent}%</strong><span>Disponibilidad</span></article><article><strong>{quality.reportingSensors}/{quality.activeSensors}</strong><span>Sensores reportando</span></article><article><strong>{quality.invalid}</strong><span>Lecturas observadas</span></article></div>}
    <section className="m-quality s1-aggregates">{aggregates.map(row => <article key={row.sensorId}><strong>{fmt(row.average)} {row.unitSymbol}</strong><span>{row.sensorName} · mín. {fmt(row.minimum)} · máx. {fmt(row.maximum)} · {row.count} lecturas</span></article>)}</section>
    <section className="m-panel s1-history"><header><div><small>SERIE DE TIEMPO</small><h2>Histórico por sensor</h2></div><select value={selectedSensor} onChange={event => { setSelectedSensor(event.target.value); setPage(1) }}><option value="all">Todos los sensores</option>{sensors.map(sensor => <option key={sensor.id} value={sensor.id}>{sensor.name}</option>)}</select></header><div className="s1-chart"><ResponsiveContainer width="100%" height="100%"><LineChart data={chart}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="time" minTickGap={28} /><YAxis /><Tooltip /><Legend /><Line type="monotone" dataKey="value" name="Lectura" stroke="#087f6a" strokeWidth={2} dot={false} /></LineChart></ResponsiveContainer></div></section>
    <section className="m-panel m-readings"><header><div><small>HISTORIAL PAGINADO</small><h2>Lecturas recibidas</h2><span>{pageInfo.total} registros · página {page} de {pageInfo.totalPages}</span></div><div className="s1-pagination"><button disabled={page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>← Anterior</button><button disabled={page >= pageInfo.totalPages} onClick={() => setPage(value => Math.min(pageInfo.totalPages, value + 1))}>Siguiente →</button></div></header>{items.map(reading => <div key={reading.id}><span className={reading.isValid ? 'm-valid' : 'm-invalid'}>{reading.validationStatus}</span><b>{reading.sensorName}</b><strong>{fmt(reading.value)} {reading.unitSymbol}</strong><time>{when(reading.capturedAtUtc)}</time>{simulated(reading) && <em className="s1-simulated">Simulado</em>}</div>)}</section>
  </>
}