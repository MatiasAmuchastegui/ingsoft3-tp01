/**
 * Cómo se muestran números y fechas en pantalla.
 *
 * Los formateadores se crean UNA vez a nivel de módulo y no adentro de las funciones: armar un
 * `Intl.NumberFormat` es caro, y estas funciones se llaman una vez por celda de cada tabla.
 * Creándolos acá, el costo se paga una sola vez para toda la aplicación.
 */

const formateadorMoneda = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  maximumFractionDigits: 2,
})

const formateadorFecha = new Intl.DateTimeFormat('es-AR', {
  dateStyle: 'short',
  timeStyle: 'short',
})

/**
 * Formatea un importe en pesos argentinos.
 *
 * Devuelve un guión largo cuando no hay valor, y no "$ 0": no es lo mismo un movimiento que no
 * tiene precio —una entrada de mercadería, un ajuste— que una venta de cero pesos.
 */
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
