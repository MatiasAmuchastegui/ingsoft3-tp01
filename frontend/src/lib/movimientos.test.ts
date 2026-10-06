import { describe, expect, it } from 'vitest'
import { calcularResultante, destinosPosibles, excedeStock } from './movimientos'
import type { Local } from '../api/tipos'

describe('calcularResultante', () => {
  // ── PARAMETRIZADO: el mismo comportamiento con todos los tipos de movimiento ──
  it.each([
    ['Entrada', 10, 3, 13],
    ['Venta', 10, 3, 7],
    ['Salida', 10, 3, 7],
    ['TransferenciaEntrada', 10, 3, 13],
    ['TransferenciaSalida', 10, 3, 7],
  ] as const)('%s de %i unidades sobre %i deja %i', (tipo, actual, cantidad, esperado) => {
    expect(calcularResultante(actual, tipo, cantidad)).toBe(esperado)
  })

  it('vender todo lo que hay deja el local en cero', () => {
    expect(calcularResultante(3, 'Venta', 3)).toBe(0)
  })
})

describe('excedeStock', () => {
  // ── CASO DE ERROR: la regla 2, el stock no puede quedar negativo ──
  it('vender más unidades de las que hay excede el stock', () => {
    expect(excedeStock(3, 'Venta', 4)).toBe(true)
  })

  // El borde exacto: es el que distingue `< 0` de `<= 0`. Sin este test, invertir esa
  // comparación no rompería nada y la pantalla rechazaría ventas perfectamente válidas.
  it('vender EXACTAMENTE lo que hay no excede', () => {
    expect(excedeStock(3, 'Venta', 3)).toBe(false)
  })

  it.each([
    ['Venta', 0, 1],
    ['Salida', 2, 5],
    ['TransferenciaSalida', 1, 2],
  ] as const)('%s de %i unidades teniendo %i excede', (tipo, actual, cantidad) => {
    expect(excedeStock(actual, tipo, cantidad)).toBe(true)
  })

  it('una entrada nunca excede, por más grande que sea', () => {
    expect(excedeStock(0, 'Entrada', 9999)).toBe(false)
  })
})

describe('destinosPosibles', () => {
  const locales: Local[] = [
    { id: 1, nombre: 'Sucursal Centro', direccion: 'Centro 100' },
    { id: 2, nombre: 'Sucursal Nueva Córdoba', direccion: 'NC 200' },
    { id: 3, nombre: 'Sucursal Shopping', direccion: 'Shopping 300' },
  ]

  it('nunca ofrece el local de origen como destino', () => {
    const destinos = destinosPosibles(locales, 1)

    expect(destinos.map((l) => l.id)).toEqual([2, 3])
  })

  it('devuelve lista vacía si el único local es el de origen', () => {
    expect(destinosPosibles([locales[0]], 1)).toEqual([])
  })
})
