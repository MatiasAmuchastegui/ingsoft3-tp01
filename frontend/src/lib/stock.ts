import type { StockItem } from '../api/tipos'

/**
 * Cómo se le pide el stock a la API.
 *
 * Es un parámetro y no una importación directa de `api.stock.listar` a propósito: así la
 * función de abajo se puede probar pasándole un doble, sin una API levantada ni una red.
 */
export type TraerStock = (localId: number) => Promise<StockItem[]>

/**
 * Qué hay que reponer en un local.
 *
 * El cliente HTTP entra **desde afuera**. Si esta función hiciera el `fetch` adentro, no
 * habría forma de probarla sin levantar el backend — y ahí deja de ser un test unitario
 * para pasar a ser uno de integración, lento y frágil.
 *
 * Qué cuenta como "hay que reponer" lo decide el backend, que compara la cantidad contra
 * el umbral propio de cada producto: no es lo mismo quedarse con dos alianzas que con dos
 * relojes de vitrina.
 */
export async function faltantesDe(localId: number, traer: TraerStock): Promise<StockItem[]> {
  const items = await traer(localId)
  return items.filter((item) => item.stockBajo)
}

/**
 * Cuántos productos de un local están por debajo de su umbral.
 *
 * Es el contador que la pantalla de Stock muestra al lado del filtro.
 */
export function contarFaltantes(items: StockItem[]): number {
  return items.filter((item) => item.stockBajo).length
}
