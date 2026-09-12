
// Sustituye prompt() y confirm() del navegador, que rompen la estetica de la
// aplicacion y muestran en claro lo que se escribe. Los cuadros nativos ademas
// bloquean el hilo y no permiten enmascarar contrasenas ni codigos TOTP.
export interface DialogField {
  name: string
  label: string
  type?: 'text' | 'number' | 'password' | 'time' | 'otp'
  value?: string
  placeholder?: string
  required?: boolean
  hint?: string
}

interface FormRequest {
  kind: 'form'
  title: string
  message?: string
  fields: DialogField[]
  confirmLabel?: string
  resolve: (value: Record<string, string> | null) => void
}

interface ConfirmRequest {
  kind: 'confirm'
  title: string
  message?: string
  confirmLabel?: string
  danger?: boolean
  resolve: (value: Record<string, string> | null) => void
}

type Request = FormRequest | ConfirmRequest

let publish: ((request: Request | null) => void) | null = null

/** Conecta el componente anfitrion con las funciones de este modulo. */
export const setDialogPublisher = (fn: ((request: Request | null) => void) | null) => { publish = fn }

export type { Request as DialogRequest }

const enqueue = (request: Omit<FormRequest, 'resolve'> | Omit<ConfirmRequest, 'resolve'>) =>
  new Promise<Record<string, string> | null>(resolve => {
    if (!publish) { resolve(null); return }
    publish({ ...request, resolve } as Request)
  })

/** Pide uno o varios datos en un solo cuadro. Devuelve null si se cancela. */
export const askForm = (title: string, fields: DialogField[], options: { message?: string; confirmLabel?: string } = {}) =>
  enqueue({ kind: 'form', title, fields, ...options })

/** Pide un unico valor. Atajo para el caso mas comun. */
export const askValue = async (title: string, field: Omit<DialogField, 'name'> = { label: 'Valor' }) => {
  const result = await askForm(title, [{ ...field, name: 'value' }])
  return result ? result.value : null
}

/** Confirmacion de si o no. Devuelve true solo si se acepta. */
export const askConfirm = async (title: string, options: { message?: string; confirmLabel?: string; danger?: boolean } = {}) =>
  (await enqueue({ kind: 'confirm', title, ...options })) !== null
