const baseUrl = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`

export interface ExportColumn<T> {
  header: string
  value: (item: T) => string | number | null | undefined
}

/**
 * Exporta la tabla que el usuario tiene delante, con sus filtros ya aplicados.
 * El servidor solo da formato: no vuelve a consultar la base, de modo que el
 * archivo coincide con lo que se ve en pantalla.
 */
export async function exportTable<T>(
  accessToken: string,
  format: 'xlsx' | 'pdf',
  title: string,
  columns: ExportColumn<T>[],
  items: T[],
) {
  const response = await fetch(`${baseUrl}/exports/${format}`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      title,
      headers: columns.map(c => c.header),
      rows: items.map(item => columns.map(c => {
        const value = c.value(item)
        return value === null || value === undefined ? '' : String(value)
      })),
    }),
  })

  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null)
    const message = problem && typeof problem === 'object' && 'message' in problem ? String((problem as { message: unknown }).message) : 'No se pudo generar el archivo.'
    throw new Error(message)
  }

  const blob = await response.blob()
  // El nombre lo decide el servidor; se lee de Content-Disposition.
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = match?.[1] ?? `export.${format}`
  document.body.appendChild(link)
  link.click()
  link.remove()
  // Revocar de inmediato cancela descargas lentas en algunos navegadores.
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
