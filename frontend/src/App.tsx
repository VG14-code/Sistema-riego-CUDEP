// eslint-disable-next-line @typescript-eslint/ban-ts-comment -- TODO: retirar al completar el tipado heredado.
// @ts-nocheck
// TODO: tipar contratos heredados de API, props y estado antes de retirar esta supresión.
import { useState } from 'react'
import { api } from './api'

const Leaf = () => <svg viewBox="0 0 24 24" aria-hidden="true" className="size-7"><path fill="currentColor" d="M20.8 3.2C14.4 3 8.2 5.7 5.2 10.5c-2.1 3.4-.8 6.8 1.3 8.1 2.3 1.4 5.5.4 7.2-2.4 1.8-2.8 2-6 6.8-11.8.4-.5.6-.8.3-1.2ZM5 21c1.2-4.7 4.2-8.4 9.2-11.2-4.1 3.6-5.7 7.1-6.2 11.2H5Z"/></svg>

function AccessCard({ onLogin }) {
  const [mode, setMode] = useState('login'); const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false)
  const submit = async (event) => {
    event.preventDefault(); setBusy(true); setMessage('')
    try {
      if (mode === 'forgot') { const result = await api.forgot(email); setMessage(result.message); return }
      onLogin(await api.login(email, password))
    } catch (error) { setMessage(error.message) } finally { setBusy(false) }
  }
  return <main className="access-shell">
    <section className="story-panel">
      <div className="brand"><span className="brand-icon"><Leaf /></span><span>CUDEP · Agricultura inteligente</span></div>
      <div className="story-copy"><p className="eyebrow">Control preciso. Agua responsable.</p><h1>Cada gota,<br/><em>justo a tiempo.</em></h1><p>Supervisa el cultivo y opera el riego desde un entorno seguro, conectado y preparado para el trabajo de campo.</p></div>
      <div className="system-note"><span className="pulse"/> Plataforma de monitoreo disponible</div>
    </section>
    <section className="form-panel"><div className="form-card">
      <div className="mobile-brand"><span className="brand-icon"><Leaf /></span>Sistema de Riego</div>
      <p className="step">ACCESO SEGURO</p><h2>{mode === 'login' ? 'Bienvenido de nuevo' : 'Recuperar acceso'}</h2>
      <p className="subtitle">{mode === 'login' ? 'Ingresa tus credenciales institucionales.' : 'Te ayudaremos a restablecer tu contraseña.'}</p>
      <form onSubmit={submit}>
        <label>Correo electrónico<input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="nombre@institucion.edu.gt" required autoComplete="email"/></label>
        {mode === 'login' && <label>Contraseña<input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="••••••••••" required autoComplete="current-password"/></label>}
        {message && <div className="notice" role="status">{message}</div>}
        <button className="primary" disabled={busy}>{busy ? 'Procesando…' : mode === 'login' ? 'Iniciar sesión' : 'Solicitar recuperación'}<span>→</span></button>
      </form>
      <button className="text-button" onClick={() => { setMode(mode === 'login' ? 'forgot' : 'login'); setMessage('') }}>{mode === 'login' ? '¿Olvidaste tu contraseña?' : 'Volver al inicio de sesión'}</button>
      <p className="security">Tus credenciales se transmiten de forma segura y las sesiones expiran automáticamente.</p>
    </div></section>
  </main>
}

function Dashboard({ session, onLogout }) {
  const roles = session.user.roles.join(' · ')
  return <main className="dashboard"><header><div className="brand dark"><span className="brand-icon"><Leaf /></span><span>Sistema de Riego</span></div><button className="logout" onClick={onLogout}>Cerrar sesión</button></header>
    <section className="welcome"><div><p className="eyebrow">SESIÓN ACTIVA</p><h1>Buen día, {session.user.fullName.split(' ')[0]}</h1><p>El módulo de acceso está conectado y listo para proteger las próximas funciones del sistema.</p></div><span className="role-chip">{roles}</span></section>
    <section className="status-grid"><article><span>01</span><h3>Identidad verificada</h3><p>La sesión utiliza un token de acceso con expiración controlada.</p></article><article><span>02</span><h3>Renovación segura</h3><p>El token de renovación se conserva para mantener la continuidad.</p></article><article><span>03</span><h3>Permisos por rol</h3><p>Las acciones se habilitan según Administrador, Técnico u Operador.</p></article></section>
  </main>
}

export default function App() {
  const [session, setSession] = useState(() => { try { return JSON.parse(sessionStorage.getItem('riego.session')) } catch { return null } })
  const login = data => { sessionStorage.setItem('riego.session', JSON.stringify(data)); setSession(data) }
  const logout = async () => { try { await api.logout(session.accessToken, session.refreshToken) } finally { sessionStorage.removeItem('riego.session'); setSession(null) } }
  return session ? <Dashboard session={session} onLogout={logout}/> : <AccessCard onLogin={login}/>
}
