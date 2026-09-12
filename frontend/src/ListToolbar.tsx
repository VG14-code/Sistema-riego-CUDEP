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
}

/** Barra de busqueda y paginacion compartida por las listas de tarjetas. */
export default function ListToolbar({ query, onQuery, page, pageCount, onPage, from, to, matches, total, placeholder = 'Buscar…', empty = 'Sin registros.' }: Props) {
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
