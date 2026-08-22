const baseUrl = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`

export type IoTRecord = Record<string, unknown>
type RequestBody = Record<string, unknown>

async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...options.headers,
      },
    })
  } catch {
    throw new Error('No se pudo conectar con el servidor. Inicia la API del sistema e inténtalo nuevamente.')
  }

  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) {
    const message = data && typeof data === 'object' && 'message' in data && typeof data.message === 'string'
      ? data.message
      : `Solicitud rechazada (${response.status}).`
    throw new Error(message)
  }
  return data as T
}

const json = (body: RequestBody): Pick<RequestInit, 'body'> => ({ body: JSON.stringify(body) })

export const week3Api = {
  summary: (token: string) => request<IoTRecord>('/iot/summary', {}, token),
  nodes: (token: string) => request<IoTRecord[]>('/iot/nodes', {}, token),
  devices: (token: string) => request<IoTRecord[]>('/iot/devices', {}, token),
  sensors: (token: string) => request<IoTRecord[]>('/iot/sensors', {}, token),
  communication: (token: string) => request<IoTRecord>("/iot/communication", {}, token),
  installations: (token: string) => request<IoTRecord[]>("/iot/traceability/installations", {}, token),
  remoteConfigurations: (token: string) => request<IoTRecord[]>("/iot/traceability/remote-configurations", {}, token),
  firmwareHistory: (token: string) => request<IoTRecord[]>("/iot/traceability/firmware", {}, token),
  inventory: (token: string) => request<IoTRecord[]>("/iot/traceability/inventory", {}, token),
  zones: (token: string) => request<IoTRecord[]>("/manual-irrigation/zones", {}, token),
  calibrations: (token: string) => request<IoTRecord[]>('/iot/calibrations', {}, token),
  catalogs: (token: string, kind: string) => request<IoTRecord[]>(`/catalogs/${kind}`, {}, token),
  createNode: (token: string, body: RequestBody) => request<IoTRecord>('/iot/nodes', { method: 'POST', ...json(body) }, token),
  updateNode: (token: string, id: number, body: RequestBody) => request<IoTRecord>(`/iot/nodes/${id}`, { method: 'PUT', ...json(body) }, token),
  deactivateNode: (token: string, id: number) => request<void>(`/iot/nodes/${id}/deactivate`, { method: 'PATCH' }, token),
  createDevice: (token: string, body: RequestBody) => request<IoTRecord>('/iot/devices', { method: 'POST', ...json(body) }, token),
  updateDevice: (token: string, id: number, body: RequestBody) => request<IoTRecord>(`/iot/devices/${id}`, { method: 'PUT', ...json(body) }, token),
  deactivateDevice: (token: string, id: number) => request<void>(`/iot/devices/${id}/deactivate`, { method: 'PATCH' }, token),
  createSensor: (token: string, body: RequestBody) => request<IoTRecord>('/iot/sensors', { method: 'POST', ...json(body) }, token),
  updateSensor: (token: string, id: number, body: RequestBody) => request<IoTRecord>(`/iot/sensors/${id}`, { method: 'PUT', ...json(body) }, token),
  deactivateSensor: (token: string, id: number) => request<void>(`/iot/sensors/${id}/deactivate`, { method: 'PATCH' }, token),
  createInstallation: (token: string, body: RequestBody) => request<IoTRecord>("/iot/traceability/installations", { method: "POST", ...json(body) }, token),
  queueRemoteConfiguration: (token: string, body: RequestBody) => request<IoTRecord>("/iot/traceability/remote-configurations", { method: "POST", ...json(body) }, token),
  registerFirmware: (token: string, body: RequestBody) => request<IoTRecord>("/iot/traceability/firmware", { method: "POST", ...json(body) }, token),
  updateInventory: (token: string, id: string, body: RequestBody) => request<void>("/iot/traceability/inventory/"+id, { method: "PATCH", ...json(body) }, token),
  calibrate: (token: string, body: RequestBody) => request<IoTRecord>('/iot/calibrations', { method: 'POST', ...json(body) }, token),
}

