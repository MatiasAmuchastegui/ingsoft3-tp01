const formateadorMoneda = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  maximumFractionDigits: 2,
})

const formateadorFecha = new Intl.DateTimeFormat('es-AR', {
  dateStyle: 'short',
  timeStyle: 'short',
})

export function pesos(valor: number | null | undefined): string {
  if (valor === null || valor === undefined) return '—'
  return formateadorMoneda.format(valor)
}

/**
 * El backend manda las fechas en UTC; se muestran en la hora local del navegador.
 * El sufijo 'Z' se agrega si falta, porque sin él el navegador la interpretaría como local.
 */
export function fechaHora(isoUtc: string): string {
  const normalizado = isoUtc.endsWith('Z') ? isoUtc : `${isoUtc}Z`
  return formateadorFecha.format(new Date(normalizado))
}
