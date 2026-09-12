import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

/**
 * La CSP se genera al compilar para que connect-src siga a la API configurada.
 * Con VITE_API_URL se permite exactamente ese origen (y su equivalente ws/wss para
 * SignalR); sin ella los clientes usan el puerto 5080 del host desde el que se abrio
 * la pagina, asi que se permite ese puerto en cualquier host en lugar de fijar localhost.
 * @param {string | undefined} apiUrl
 * @returns {import('vite').Plugin}
 */
function contentSecurityPolicy(apiUrl) {
  let api = ['http://*:5080', 'ws://*:5080']
  if (apiUrl) {
    try {
      const origin = new URL(apiUrl).origin
      api = [origin, origin.replace(/^http/, 'ws')]
    } catch {
      api = [] // URL relativa: la API se sirve desde el mismo origen y basta con 'self'.
    }
  }
  const policy = [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    // Los mosaicos se sirven desde tile.openstreetmap.org; el comodin *. no cubre el dominio sin subdominio.
    "img-src 'self' data: https://tile.openstreetmap.org https://*.tile.openstreetmap.org",
    `connect-src 'self' ${api.join(' ')}`.trim(),
    // Hay dependencias que crean su worker desde un blob; sin esto caia al fallback de script-src y se bloqueaba.
    "worker-src 'self' blob:",
    "font-src 'self' data:",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    // frame-ancestors no va aqui: el navegador lo ignora en <meta> y debe enviarse como cabecera HTTP.
  ].join('; ')
  return {
    name: 'riego-content-security-policy',
    transformIndexHtml: () => [{ tag: 'meta', attrs: { 'http-equiv': 'Content-Security-Policy', content: policy }, injectTo: 'head' }],
  }
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  return { plugins: [react(), tailwindcss(), contentSecurityPolicy(env.VITE_API_URL)], server: { port: 5173 } }
})
