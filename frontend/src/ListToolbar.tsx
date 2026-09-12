import { useState } from 'react'

interface Props {
  query: string
  onQuery: (value: string) => void
  page: number
  pageCount: number
  onPage: (value: number) => void
  from: number
  to: number
  matches: number
  total: number
  placeholder?: string
  empty?: string
  /** Exporta los registros filtrados; sin esto no se muestran los botones. */
  onExport?: (format: 'xlsx' | 'pdf') => Promise<void>
}

/** Barra de busqueda y paginacion compartida por las listas de tarjetas. */
export default function ListToolbar({ query, onQuery, page, pageCount, onPage, from, to, matches, total, placeholder = 'Buscar…', empty = 'Sin registros.', onExport }: Props) {
  const [busy, setBusy] = useState<'xlsx' | 'pdf' | null>(null)
  const run = async (format: 'xlsx' | 'pdf') => { setBusy(format); try { await onExport?.(format) } finally { setBusy(null) } }
  return (
    <div className="lt">
      <div className="lt-search">
        <span aria-hidden="true">⌕</span>
        <input type="search" value={query} onChange={event => onQuery(event.target.value)} placeholder={placeholder} aria-label={placeholder} />
        {query && <button type="button" onClick={() => onQuery('')} aria-label="Limpiar búsqueda">×</button>}
      </div>
      <p className="lt-count">
        {matches === 0
          ? (query ? `Sin coincidencias para “${query}”.` : empty)
          : <>Mostrando <b>{from}–{to}</b> de <b>{matches}</b>{matches !== total && <> (filtrados de {total})</>}</>}
      </p>
      {onExport && (
        <div className="lt-export">
          <button type="button" onClick={() => run('xlsx')} disabled={busy !== null || matches === 0} title="Exportar los registros filtrados a Excel">{busy === 'xlsx' ? 'Generando…' : 'Excel'}</button>
          <button type="button" onClick={() => run('pdf')} disabled={busy !== null || matches === 0} title="Exportar los registros filtrados a PDF">{busy === 'pdf' ? 'Generando…' : 'PDF'}</button>
        </div>
      )}
      {pageCount > 1 && (
        <div className="lt-pages">
          <button type="button" onClick={() => onPage(page - 1)} disabled={page <= 1}>‹ Anterior</button>
          <span>Página {page} de {pageCount}</span>
          <button type="button" onClick={() => onPage(page + 1)} disabled={page >= pageCount}>Siguiente ›</button>
        </div>
      )}
    </div>
  )
}
