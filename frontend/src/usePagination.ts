import { useEffect, useMemo, useState } from 'react'

/**
 * Busqueda por coincidencia y paginacion para las listas de tarjetas.
 * Las pantallas volcaban todos los registros de golpe: la de sesiones llega a
 * cientos de filas y la de etapas crece con cada cultivo.
 */
export function usePagination<T>(items: T[], searchable: (item: T) => string, pageSize = 8) {
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase()
    if (!needle) return items
    return items.filter(item => searchable(item).toLowerCase().includes(needle))
  }, [items, query, searchable])

  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize))

  // Al filtrar o al borrar registros la pagina actual puede quedar fuera de rango.
  useEffect(() => { if (page > pageCount) setPage(pageCount) }, [page, pageCount])

  const from = (Math.min(page, pageCount) - 1) * pageSize
  const visible = filtered.slice(from, from + pageSize)

  return {
    query,
    setQuery: (value: string) => { setQuery(value); setPage(1) },
    page: Math.min(page, pageCount),
    pageCount,
    setPage,
    visible,
    /** Todos los que pasan el filtro, no solo la pagina visible: es lo que se exporta. */
    filtered,
    total: items.length,
    matches: filtered.length,
    from: filtered.length === 0 ? 0 : from + 1,
    to: from + visible.length,
  }
}
