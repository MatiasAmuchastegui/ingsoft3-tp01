import { describe, expect, it, vi } from 'vitest'
import { contarFaltantes, faltantesDe, urgenciaDeReposicion } from './stock'
import type { StockItem } from '../api/tipos'

/** Arma un StockItem con lo mínimo que estas reglas miran. */
function item(productoId: number, stockBajo: boolean): StockItem {
  return {
    productoId,
    sku: `REL-000${productoId}`,
    productoNombre: `Producto ${productoId}`,
    categoriaNombre: 'Relojes',
    localId: 1,
    localNombre: 'Sucursal Centro',
    cantidad: stockBajo ? 1 : 20,
    umbralStockBajo: 5,
    stockBajo,
    precioBase: 1000,
  }
}

describe('faltantesDe', () => {
  // ── MOCK: el cliente HTTP reemplazado por un doble ──
  // Sin esto habría que levantar el backend y la base para probar un filtro.

  it('devuelve sólo los productos por debajo de su umbral', async () => {
    const traer = vi.fn().mockResolvedValue([item(1, true), item(2, false), item(3, true)])

    const faltantes = await faltantesDe(1, traer)

    expect(faltantes.map((i) => i.productoId)).toEqual([1, 3])
  })

  // Éste es el que convierte al doble en un MOCK y no en un stub: el assert no mira lo que
  // la función devolvió, mira QUÉ LE PIDIÓ a la dependencia. Si mañana alguien cambia el
  // parámetro sin querer, se pone rojo.
  it('le pide el stock del local que recibió, una sola vez', async () => {
    const traer = vi.fn().mockResolvedValue([])

    await faltantesDe(7, traer)

    expect(traer).toHaveBeenCalledWith(7)
    expect(traer).toHaveBeenCalledTimes(1)
  })

  it('devuelve lista vacía si el local no tiene faltantes', async () => {
    const traer = vi.fn().mockResolvedValue([item(1, false), item(2, false)])

    expect(await faltantesDe(1, traer)).toEqual([])
  })

  it('propaga el error si la API falla, en lugar de devolver una lista vacía', async () => {
    // Que un fallo de red se vea como "no hay nada que reponer" sería peor que el error.
    const traer = vi.fn().mockRejectedValue(new Error('Error 504'))

    await expect(faltantesDe(1, traer)).rejects.toThrow('Error 504')
  })
})

describe('contarFaltantes', () => {
  it.each([
    [[], 0],
    [[item(1, false)], 0],
    [[item(1, true)], 1],
    [[item(1, true), item(2, false), item(3, true)], 2],
  ])('cuenta %# correctamente', (items, esperado) => {
    expect(contarFaltantes(items as StockItem[])).toBe(esperado)
  })
})

describe('urgenciaDeReposicion', () => {
  // Un test por cada camino que la función declara. No es "escribí muchos tests":
  // es "cubrí lo que declaraste" — los cinco return son cinco comportamientos.
  it.each([
    [0, 10, 'sin-stock'],   // sin unidades: frena la venta
    [4, 10, 'critica'],     // en la mitad del umbral o menos
    [5, 10, 'critica'],     // el borde exacto de la mitad
    [8, 10, 'baja'],        // por debajo del umbral
    [10, 10, 'baja'],       // el borde exacto del umbral
    [15, 10, 'normal'],     // hasta el doble
    [20, 10, 'normal'],     // el borde exacto del doble
    [50, 10, 'holgada'],    // por encima
  ])('%i unidades con umbral %i es %s', (cantidad, umbral, esperado) => {
    expect(urgenciaDeReposicion(cantidad, umbral)).toBe(esperado)
  })

  // Los bordes importan: sin ellos, cambiar un <= por un < no rompería nada.
  it('un umbral de cero deja todo en holgada menos el cero', () => {
    expect(urgenciaDeReposicion(0, 0)).toBe('sin-stock')
    expect(urgenciaDeReposicion(1, 0)).toBe('holgada')
  })
})
