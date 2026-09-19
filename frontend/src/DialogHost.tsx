import { useEffect, useRef, useState } from 'react'
import { setDialogPublisher, type DialogRequest } from './dialogs'

/** Se monta una sola vez en la raiz de la aplicacion. */
export function DialogHost() {
  const [request, setRequest] = useState<DialogRequest | null>(null)
  const [values, setValues] = useState<Record<string, string>>({})
  const [error, setError] = useState('')
  const firstField = useRef<HTMLInputElement>(null)

  useEffect(() => { setDialogPublisher(setRequest); return () => setDialogPublisher(null) }, [])

  useEffect(() => {
    if (!request) return
    setError('')
    setValues(request.kind === 'form' ? Object.fromEntries(request.fields.map(f => [f.name, f.value ?? ''])) : {})
    const focus = window.setTimeout(() => firstField.current?.focus(), 40)
    return () => window.clearTimeout(focus)
  }, [request])

  if (!request) return null

  const close = (result: Record<string, string> | null) => { request.resolve(result); setRequest(null) }

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    if (request.kind === 'confirm') { close({}); return }
    const missing = request.fields.find(f => f.required !== false && !String(values[f.name] ?? '').trim())
    if (missing) { setError(`Completa el campo “${missing.label}”.`); return }
    close(values)
  }

  const danger = request.kind === 'confirm' && request.danger

  return (
    <div className="dlg-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) close(null) }}>
      <div className="dlg" role="dialog" aria-modal="true" aria-label={request.title} onKeyDown={event => { if (event.key === 'Escape') close(null) }}>
        <form onSubmit={submit}>
          <h2>{request.title}</h2>
          {request.message && <p className="dlg-message">{request.message}</p>}
          {request.kind === 'form' && request.fields.map((field, index) => (
            <label key={field.name}>
              {field.label}
              {field.type === 'select' ? (
                <select
                  value={values[field.name] ?? ''}
                  onChange={event => setValues({ ...values, [field.name]: event.target.value })}
                >
                  {(field.options ?? []).map(option => { const value = typeof option === 'string' ? option : option.value; const label = typeof option === 'string' ? option : option.label; return <option key={value} value={value}>{label}</option> })}
                </select>
              ) : (
              <input
                ref={index === 0 ? firstField : undefined}
                type={field.type === 'otp' ? 'text' : field.type ?? 'text'}
                inputMode={field.type === 'otp' || field.type === 'number' ? 'numeric' : undefined}
                autoComplete={field.type === 'password' ? 'new-password' : field.type === 'otp' ? 'one-time-code' : 'off'}
                maxLength={field.type === 'otp' ? 6 : undefined}
                placeholder={field.placeholder}
                value={values[field.name] ?? ''}
                onChange={event => setValues({ ...values, [field.name]: event.target.value })}
              />
              )}
              {field.hint && <small>{field.hint}</small>}
            </label>
          ))}
          {error && <p className="dlg-error" role="alert">{error}</p>}
          <div className="dlg-actions">
            <button type="button" className="dlg-cancel" onClick={() => close(null)}>Cancelar</button>
            <button type="submit" className={danger ? 'dlg-danger' : ''}>{request.confirmLabel ?? (request.kind === 'confirm' ? 'Confirmar' : 'Guardar')}</button>
          </div>
        </form>
      </div>
    </div>
  )
}
