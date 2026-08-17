export interface AuthUser {
  id: string
  email: string
  fullName: string
  roles: string[]
}

export interface AuthSession {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
  user: AuthUser
}

interface ApiError { message?: string }

const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080/api'

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...options.headers },
  })
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) throw new Error((data as ApiError | null)?.message ?? 'No fue posible completar la solicitud.')
  return data as T
}

export const api = {
  login: (email: string, password: string) => request<AuthSession>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  forgot: (email: string) => request<{ message: string }>('/auth/forgot-password', { method: 'POST', body: JSON.stringify({ email }) }),
  logout: (accessToken: string, refreshToken: string) => request<void>('/auth/logout', { method: 'POST', headers: { Authorization: `Bearer ${accessToken}` }, body: JSON.stringify({ refreshToken }) }),
}
