const baseUrl = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`

export interface UserSummary { id: string; email: string; fullName: string; status: string; roles: string[] }
export interface AuthSession { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string; user: UserSummary }
export interface Role { name: string; description: string; permissions: string[] }
export interface Permission { id: string; code: string; description: string }
export interface CatalogItem { id: string; kind: string; code: string; name: string; description: string | null; symbol: string | null; isActive: boolean }
export interface CatalogForm { code: string; name: string; description?: string | null; symbol?: string | null; isActive: boolean }
export interface DeviceBrand { id: string; code: string; name: string; description: string | null; isActive: boolean; modelCount: number }
export interface DeviceModel { id: string; deviceBrandId: string; brand: string; deviceTypeId: string; deviceType: string; code: string; name: string; description: string | null; isActive: boolean; deviceCount: number }
export interface TotpStatus { enabled: boolean; hasAuthenticator: boolean }
export interface TotpSetup { sharedKey: string; authenticatorUri: string }
export interface GlobalParameter { id: string; key: string; value: string; dataType: string; category: string; description: string; isEditable: boolean }
export interface AuditItem { id: number; eventType: string; detail: string; occurredAtUtc: string; userEmail: string | null }
type JsonBody = Record<string, unknown>

async function request<T>(path: string, options: RequestInit = {}, accessToken?: string, totpCode?: string): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}), ...(totpCode ? { 'X-TOTP-Code': totpCode } : {}), ...options.headers } })
  } catch { throw new Error('No se pudo conectar con el servidor. Inicia la API del sistema e inténtalo nuevamente.') }
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) {
    const message = data && typeof data === 'object' && 'message' in data && typeof data.message === 'string' ? data.message : `Solicitud rechazada (${response.status}).`
    throw new Error(message)
  }
  return data as T
}
const json = (body: JsonBody): Pick<RequestInit, 'body'> => ({ body: JSON.stringify(body) })

export const week2Api = {
  login: (email: string, password: string) => request<AuthSession>('/auth/login', { method: 'POST', ...json({ email, password }) }),
  forgot: (email: string) => request<unknown>('/auth/forgot-password', { method: 'POST', ...json({ email }) }),
  logout: (session: AuthSession) => request<void>('/auth/logout', { method: 'POST', ...json({ refreshToken: session.refreshToken }) }, session.accessToken),
  createUser: (token: string, body: { email: string; password: string; fullName: string }, totp: string) => request<UserSummary>('/auth/register', { method: 'POST', ...json(body) }, token, totp),
  users: (token: string) => request<UserSummary[]>('/users', {}, token),
  roles: (token: string) => request<Role[]>('/roles', {}, token),
  permissions: (token: string) => request<Permission[]>('/roles/permissions', {}, token),
  setUserStatus: (token: string, id: string, status: string, totp: string) => request<void>(`/users/${id}/status`, { method: 'PATCH', ...json({ status }) }, token, totp),
  setUserRoles: (token: string, id: string, roles: string[], totp: string) => request<void>(`/users/${id}/roles`, { method: 'PUT', ...json({ roles }) }, token, totp),
  setRolePermissions: (token: string, role: string, permissions: string[], totp: string) => request<void>(`/roles/${encodeURIComponent(role)}/permissions`, { method: 'PUT', ...json({ permissions }) }, token, totp),
  totpStatus: (token: string) => request<TotpStatus>('/security/2fa/status', {}, token),
  totpSetup: (token: string) => request<TotpSetup>('/security/2fa/setup', { method: 'POST' }, token),
  totpEnable: (token: string, code: string) => request<{ enabled: boolean; recoveryCodes: string[] }>('/security/2fa/enable', { method: 'POST', ...json({ code }) }, token),
  totpDisable: (token: string, code: string) => request<void>('/security/2fa/disable', { method: 'POST', ...json({ code }) }, token),
  catalogs: (token: string, kind: string) => request<CatalogItem[]>(`/catalogs/${kind}`, {}, token),
  createCatalog: (token: string, kind: string, item: CatalogForm) => request<CatalogItem>(`/catalogs/${kind}`, { method: 'POST', ...json(item as unknown as JsonBody) }, token),
  updateCatalog: (token: string, kind: string, id: string, item: CatalogForm) => request<CatalogItem>(`/catalogs/${kind}/${id}`, { method: 'PUT', ...json(item as unknown as JsonBody) }, token),
  deleteCatalog: (token: string, kind: string, id: string) => request<void>(`/catalogs/${kind}/${id}`, { method: 'DELETE' }, token),
  brands: (token: string) => request<DeviceBrand[]>('/device-catalogs/brands', {}, token),
  models: (token: string) => request<DeviceModel[]>('/device-catalogs/models', {}, token),
  createBrand: (token: string, body: JsonBody) => request<unknown>('/device-catalogs/brands', { method: 'POST', ...json(body) }, token),
  updateBrand: (token: string, id: string, body: JsonBody) => request<void>(`/device-catalogs/brands/${id}`, { method: 'PUT', ...json(body) }, token),
  deleteBrand: (token: string, id: string) => request<void>(`/device-catalogs/brands/${id}`, { method: 'DELETE' }, token),
  createModel: (token: string, body: JsonBody) => request<unknown>('/device-catalogs/models', { method: 'POST', ...json(body) }, token),
  updateModel: (token: string, id: string, body: JsonBody) => request<void>(`/device-catalogs/models/${id}`, { method: 'PUT', ...json(body) }, token),
  deleteModel: (token: string, id: string) => request<void>(`/device-catalogs/models/${id}`, { method: 'DELETE' }, token),
  settings: (token: string) => request<GlobalParameter[]>('/settings', {}, token),
  saveSetting: (token: string, key: string, item: JsonBody) => request<void>(`/settings/${key}`, { method: 'PUT', ...json(item) }, token),
  audit: (token: string) => request<AuditItem[]>('/audit?take=100', {}, token),
}
