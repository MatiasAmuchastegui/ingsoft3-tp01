/**
 * Reglas de precio para la pantalla de Stock.
 *
 * Nota: este archivo entra SIN tests a propósito. Es la demostración de que el umbral de
 * cobertura frena el merge aunque todo compile y todos los tests estén en verde.
 */

/** Cuánto vale el stock de un producto en un local. */
export function valorDelStock(cantidad: number, precioUnitario: number): number {
  return Math.round(cantidad * precioUnitario * 100) / 100
}

/**
 * En qué franja de precio cae un producto, para poder agrupar el catálogo.
 *
 * Cinco caminos, ninguno cubierto por un test.
 */
export function franjaDePrecio(precio: number): string {
  if (precio <= 0) {
    return 'sin-precio'
  }

  if (precio < 10000) {
    return 'economica'
  }

  if (precio < 50000) {
    return 'media'
  }

  if (precio < 200000) {
    return 'alta'
  }

  return 'premium'
}
