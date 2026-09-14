import { useEffect, useState } from 'react'
import { Area, AreaChart, CartesianGrid, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import './energy.css'

interface Session { accessToken: string }
interface Props { session: Session; notify: (message: string) => void }
interface EnergyPoint { capturedAtUtc: string; generationWatts: number; batteryPercent: number; consumptionWatts: number; batteryVoltage: number }
interface EnergyStatus {
  name: string; status: string
  array: { name: string; ratedPowerWatts: number; panelCount: number }
  battery: { name: string; capacityWattHours: number; currentChargePercent: number; minimumSafeChargePercent: number; status: string }
  reading: EnergyPoint | null
}

const root = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`
async function get<T>(path: string, token: string): Promise<T> {
  const response = await fetch(`${root}${path}`, { headers: { Authorization: `Bearer ${token}` } })
  const data: unknown = await response.json().catch(() => null)
  if (!response.ok) throw new Error(`No fue posible consultar energía (${response.status}).`)
  return data as T
}
const watts = (value?: number) => `${Number(value ?? 0).toLocaleString('es-GT', { maximumFractionDigits: 0 })} W`

export default function EnergyPanel({ session, notify }: Props) {
  const [status, setStatus] = useState<EnergyStatus | null>(null)
  const [history, setHistory] = useState<EnergyPoint[]>([])
  useEffect(() => {
    Promise.all([get<EnergyStatus>('/energy/status', session.accessToken), get<EnergyPoint[]>('/energy/history?hours=24', session.accessToken)])
      .then(([nextStatus, nextHistory]) => { setStatus(nextStatus); setHistory(nextHistory) })
      .catch((error: Error) => notify(error.message))
  }, [session.accessToken, notify])
  if (!status) return <section className="energy-empty">Esperando la primera lectura energética…</section>
  const current = status.reading
  return <div className="energy-page">
    <header><p>MÓDULO 12 · ENERGÍA SOLAR</p><h1>Autonomía energética</h1><span>Generación, batería y demanda de la infraestructura IoT.</span></header>
    <section className="energy-kpis">
      <article><small>GENERACIÓN</small><b>{watts(current?.generationWatts)}</b><span>{status.array.panelCount} paneles · {watts(status.array.ratedPowerWatts)} nominales</span></article>
      <article><small>BATERÍA</small><b>{Number(status.battery.currentChargePercent).toFixed(1)}%</b><span>{status.battery.status} · mínimo {status.battery.minimumSafeChargePercent}%</span></article>
      <article><small>CONSUMO</small><b>{watts(current?.consumptionWatts)}</b><span>{Number(current?.batteryVoltage ?? 0).toFixed(1)} V · {status.name}</span></article>
    </section>
    <section className="energy-chart"><div><small>ÚLTIMAS 24 HORAS</small><h2>Balance solar y carga</h2></div><ResponsiveContainer width="100%" height={330}><AreaChart data={history}><defs><linearGradient id="solar" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor="#efb64a" stopOpacity={.5}/><stop offset="95%" stopColor="#efb64a" stopOpacity={0}/></linearGradient></defs><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="capturedAtUtc" tickFormatter={value => new Date(String(value)).toLocaleTimeString('es-GT', { hour: '2-digit', minute: '2-digit' })}/><YAxis/><Tooltip labelFormatter={value => new Date(String(value)).toLocaleString('es-GT')}/><Area type="monotone" dataKey="generationWatts" name="Generación W" stroke="#d99618" fill="url(#solar)"/><Line type="monotone" dataKey="consumptionWatts" name="Consumo W" stroke="#247565" dot={false}/><Line type="monotone" dataKey="batteryPercent" name="Batería %" stroke="#3569a8" dot={false}/></AreaChart></ResponsiveContainer></section>
  </div>
}
