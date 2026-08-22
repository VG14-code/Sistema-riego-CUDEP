export function humanizeInterfaceMessage(message: string) {
  const clean = message.replace(/^(?:Error|TypeError):\s*/i, '').trim()
  if (/signalr|connection was stopped during negotiation|hubconnection|negotia(?:te|tion)/i.test(clean)) return 'Se perdió temporalmente la conexión en tiempo real. Reintentando automáticamente.'
  if (/solicitud rechazada \(401\)|unauthorized|status code 401/i.test(clean)) return 'Tu sesión expiró. Inicia sesión nuevamente.'
  if (/solicitud rechazada \(403\)|forbidden|status code 403/i.test(clean)) return 'No tienes permisos para realizar esta acción.'
  if (/failed to fetch|network(?:error| request failed)|load failed|fetch failed/i.test(clean)) return 'No fue posible comunicarse con el sistema. Verifica la conexión e inténtalo nuevamente.'
  if (/unexpected token|invalid json|json parse|not valid json/i.test(clean)) return 'La respuesta del sistema no pudo interpretarse. Inténtalo nuevamente.'
  if (/abort(?:ed|error)|operation was canceled|operation was cancelled/i.test(clean)) return 'La operación se interrumpió antes de completarse.'
  return clean || 'No fue posible completar la operación.'
}