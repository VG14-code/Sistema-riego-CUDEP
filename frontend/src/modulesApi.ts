export interface Reading {
  id: number
  sensorId: string
  sensorName: string
  zoneName: string | null
  capturedAtUtc: string
  value: number
  unitSymbol: string | null
  batteryPercent: number | null
  signalStrength: number | null
  isValid: boolean
  validationStatus: string
  transport: string
}

export interface ActivityItem { type: string; detail: string; occurredAtUtc: string }
export interface DashboardData {
  activeSensors: number
  activeZones: number
  activeDevices: number
  invalidReadings: number
  activeCropCycles: number
  averageMoisture: number
  generatedAtUtc: string
  latestReadings: Reading[]
  recentActivity: ActivityItem[]
}
export interface Zone { id: string; code: string; name: string; areaHectares: number; status: string; sensor: string | null; latitude: number | null; longitude: number | null }
export interface Sector { id: string; code: string; name: string; zones: Zone[] }
export interface Block { id: string; code: string; name: string; sectors: Sector[] }
export interface Farm { id: string; code: string; name: string; latitude: number | null; longitude: number | null; blocks: Block[] }
export interface Center { id: string; code: string; name: string; location: string | null; farms: Farm[] }
export interface IoTNode { id: string; code: string; name: string; operationalStatus: string; lastCommunicationUtc: string | null; deviceCount: number; isActive: boolean }
export interface IoTDevice { id: string; code: string; name: string; operationalStatus: string; nodeId: string | null; nodeName: string | null; lastCommunicationUtc: string | null; sensorCount: number; isActive: boolean }
export interface IoTSensor { id: string; code: string; name: string; operationalStatus: string; deviceId: string | null; deviceName: string | null; lastReadingUtc: string | null; unitSymbol: string | null; isActive: boolean }
export interface Quality { total: number; valid: number; invalid: number; validPercent: number; activeSensors: number; reportingSensors: number; availabilityPercent: number }

type JsonObject = Record<string, unknown>
interface ApiError { message?: string }
const baseUrl = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`

async function call<T>(path: string, token: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, { ...options, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...options.headers } })
  } catch {
    throw new Error('La API no está disponible. Ejecuta Iniciar Sistema.ps1.')
  }
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error((data as ApiError | null)?.message ?? `Solicitud rechazada (${response.status}).`)
  return data as T
}

export const modulesApi = {
  dashboard: (token: string) => call<DashboardData>('/system/dashboard', token),
  hierarchy: (token: string) => call<Center[]>('/territory/hierarchy', token),
  nodes: (token: string) => call<IoTNode[]>('/iot/nodes', token),
  devices: (token: string) => call<IoTDevice[]>('/iot/devices', token),
  sensors: (token: string) => call<IoTSensor[]>('/iot/sensors', token),
  telemetry: (token: string, query = '?take=250') => call<Reading[]>(`/telemetry/history${query}`, token),
  quality: (token: string) => call<Quality>('/telemetry/quality', token),
  centers: (token: string) => call<unknown[]>('/territory/centers', token),
  farms: (token: string) => call<unknown[]>('/territory/farms', token),
  blocks: (token: string) => call<unknown[]>('/territory/blocks', token),
  sectors: (token: string) => call<unknown[]>('/territory/sectors', token),
  zones: (token: string) => call<unknown[]>('/territory/zones', token),
  catalogs: (token: string, kind: string) => call<unknown[]>(`/catalogs/${kind}`, token),
  createTerritory: (token: string, kind: string, data: JsonObject) => call<unknown>(`/territory/${kind}`, token, { method: 'POST', body: JSON.stringify(data) }),
  updateTerritory: (token: string, kind: string, id: string, data: JsonObject) => call<unknown>(`/territory/${kind}/${id}`, token, { method: 'PUT', body: JSON.stringify(data) }),
  toggleTerritory: (token: string, kind: string, id: string) => call<void>(`/territory/${kind}/${id}/toggle`, token, { method: 'PATCH' }),
  soils: (token: string) => call<unknown[]>('/agronomy/soil-types', token),
  crops: (token: string) => call<unknown[]>('/agronomy/crops', token),
  requirements: (token: string) => call<unknown[]>('/agronomy/requirements', token),
  recommendations: (token: string) => call<unknown[]>('/agronomy/recommendations', token),
  cycles: (token: string) => call<unknown[]>('/crop-planning/cycles', token),
  createRequirement: (token: string, data: JsonObject) => call<unknown>('/agronomy/requirements', token, { method: 'POST', body: JSON.stringify(data) }),
  createCycle: (token: string, data: JsonObject) => call<unknown>('/crop-planning/cycles', token, { method: 'POST', body: JSON.stringify(data) }),
}
