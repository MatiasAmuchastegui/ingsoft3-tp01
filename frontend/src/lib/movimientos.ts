import type { Local, TipoMovimiento } from '../api/tipos'

/**
 * Reglas de stock del lado del cliente.
 *
 * Esta lógica vivía adentro de `ModalMovimiento` y `ModalTransferencia`, mezclada con el
 * JSX. Así no se podía probar sin montar React, que es mucha maquinaria para verificar una
 * resta. Acá son funciones puras: reciben valores, devuelven valores, y se testean en
 * milisegundos.
 *
 * Importante: esto es comodidad de interfaz, no la garantía. El backend vuelve a verificar
 * todo —y la base tiene un `CHECK (cantidad >= 0)` que es la garantía real—. Lo que se gana
 * acá es avisarle a quien carga antes de mandar el pedido, y con un mensaje mejor.
 */

/**
 * Cómo queda el stock de un local después de registrar un movimiento.
 *
 * Entrada suma; venta y salida restan. El signo lo decide el tipo, y por eso la cantidad
 * que se recibe es siempre positiva — igual que en el backend, donde `Movimiento.Cantidad`
 * nunca es negativa.
 */
export function calcularResultante(
  cantidadActual: number,
  tipo: TipoMovimiento,
  cantidad: number,
): number {
  const delta = tipo === 'Entrada' || tipo === 'TransferenciaEntrada' ? cantidad : -cantidad
  return cantidadActual + delta
}

/**
 * Regla de negocio 2: el stock de un local nunca puede quedar negativo.
 *
 * Se calcula sobre el resultado y no comparando cantidades sueltas, para que haya un solo
 * lugar donde vive el signo: si mañana cambia cómo suma un tipo, cambia en `calcularResultante`
 * y esto sigue siendo correcto.
 */
export function excedeStock(
  cantidadActual: number,
  tipo: TipoMovimiento,
  cantidad: number,
): boolean {
  return calcularResultante(cantidadActual, tipo, cantidad) < 0
}

/**
 * Los locales a los que se puede trasladar, dado el local de origen.
 *
 * El destino nunca puede ser el origen. Se resuelve sacándolo de la lista en lugar de
 * validarlo después: si no se puede elegir mal, no hace falta un mensaje que explique
 * que está mal.
 */
export function destinosPosibles(locales: Local[], localOrigenId: number): Local[] {
  return locales.filter((local) => local.id !== localOrigenId)
}
