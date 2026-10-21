import { expect, test, type APIRequestContext } from '@playwright/test'
import { credenciales, prefijoUnico } from './comun'

/**
 * Integración: la api desplegada, con su base de datos de verdad, sin navegador y sin
 * ningún doble en el medio.
 *
 * Los unitarios del TP5 SACARON la base para probar la lógica sola, con un doble en su
 * lugar. Acá la base vuelve a estar: cada prueba le pide algo a la api y después, con OTRO
 * pedido, le pregunta a la base si de verdad quedó. El doble contesta lo que uno le dijo;
 * la base, lo que pasó.
 *
 * No abre ningún navegador: el fixture `request` de Playwright es un cliente HTTP.
 */

// La api tiene su propia dirección, que NO es la del front. Sin barra al final.
const API = process.env.API_BASE_URL ?? 'http://localhost:8080'
const { email, password } = credenciales()

interface Categoria {
  id: number
  nombre: string
}

interface ItemStock {
  productoId: number
  localId: number
  cantidad: number
}

interface Movimiento {
  id: number
  observacion: string | null
  fechaUtc: string
  cantidadResultante: number
}

/** Inicia sesión y devuelve las cabeceras para los pedidos que siguen. */
async function autenticar(request: APIRequestContext) {
  const r = await request.post(`${API}/api/auth/login`, { data: { email, password } })
  expect(r.status(), 'login').toBe(200)
  const { token } = await r.json()
  return { Authorization: `Bearer ${token}` }
}

async function categorias(request: APIRequestContext, cabeceras: Record<string, string>) {
  const r = await request.get(`${API}/api/categorias`, { headers: cabeceras })
  expect(r.status()).toBe(200)
  return (await r.json()) as Categoria[]
}

/** Cuántas unidades dice la base que hay de ese producto en ese local. */
async function cantidadEnStock(request: APIRequestContext, cabeceras: Record<string, string>, item: ItemStock) {
  const r = await request.get(`${API}/api/stock?localId=${item.localId}`, { headers: cabeceras })
  expect(r.status()).toBe(200)
  const fila = ((await r.json()) as ItemStock[]).find((s) => s.productoId === item.productoId)
  expect(fila, 'el producto sigue en el stock del local').toBeDefined()
  return fila!.cantidad
}

async function registrar(
  request: APIRequestContext,
  cabeceras: Record<string, string>,
  item: ItemStock,
  tipo: 'Entrada' | 'Venta',
  observacion: string,
) {
  const r = await request.post(`${API}/api/movimientos`, {
    headers: cabeceras,
    data: { tipo, productoId: item.productoId, localId: item.localId, cantidad: 1, observacion },
  })
  expect(r.status(), `registrar ${tipo}`).toBe(200)
  return (await r.json()) as Movimiento
}

test('el alta de una categoría queda en la base, y el borrado la saca', async ({ request }) => {
  const cabeceras = await autenticar(request)
  const nombre = `api ${Date.now()}` // único: no choca con otra corrida

  const alta = await request.post(`${API}/api/categorias`, {
    headers: cabeceras,
    data: { nombre, prefijoSku: prefijoUnico() },
  })
  expect(alta.status()).toBe(201) // la api dice «creada»
  const creada = (await alta.json()) as Categoria

  // Un pedido aparte: el que contesta ahora es la base, no lo que la api acaba de devolver.
  expect((await categorias(request, cabeceras)).some((c) => c.nombre === nombre)).toBe(true)

  const borrado = await request.delete(`${API}/api/categorias/${creada.id}`, { headers: cabeceras })
  expect(borrado.status()).toBe(204) // «borrada, no hay nada que devolver»

  expect((await categorias(request, cabeceras)).some((c) => c.nombre === nombre)).toBe(false)
})

test('un nombre vacío lo rechaza la api, y no se crea nada', async ({ request }) => {
  const cabeceras = await autenticar(request)
  const antes = await categorias(request, cabeceras)

  const alta = await request.post(`${API}/api/categorias`, {
    headers: cabeceras,
    data: { nombre: '', prefijoSku: prefijoUnico() },
  })

  // 409 y no 400: esta api traduce TODA regla de negocio a 409, en un solo lugar
  // (ManejadorExcepcionesMiddleware). Ése es su contrato hoy, y es lo que se fija acá.
  expect(alta.status()).toBe(409)
  expect((await alta.json()).detail).toContain('obligatorio')

  const despues = await categorias(request, cabeceras)
  expect(despues.length).toBe(antes.length) // y de verdad no se creó nada
})

test('una entrada suma al stock en la base, y una venta lo deja como estaba', async ({ request }) => {
  const cabeceras = await autenticar(request)

  // Un producto cualquiera, en un local cualquiera. La prueba es relativa a lo que encuentra,
  // así que no depende de qué datos tenga el entorno.
  const r = await request.get(`${API}/api/stock`, { headers: cabeceras })
  expect(r.status()).toBe(200)
  const [item] = (await r.json()) as ItemStock[]
  const antes = item.cantidad
  const marca = `api ${Date.now()}`

  const entrada = await registrar(request, cabeceras, item, 'Entrada', marca)
  expect(entrada.cantidadResultante).toBe(antes + 1)
  expect(await cantidadEnStock(request, cabeceras, item)).toBe(antes + 1)

  // El movimiento quedó guardado tal cual, con su fecha. Esa columna es timestamptz, y
  // Npgsql sólo acepta escribirle fechas en UTC: si alguien cambiara la hora del registro
  // por la local, la api contestaría 500 en el alta de arriba. Un unitario no lo vería: no
  // hay ninguna base en él que pueda rechazar la fecha.
  const ultimos = await request.get(
    `${API}/api/movimientos?productoId=${item.productoId}&localId=${item.localId}&limite=1`,
    { headers: cabeceras },
  )
  expect(ultimos.status()).toBe(200)
  const [guardado] = (await ultimos.json()) as Movimiento[]
  expect(guardado.id).toBe(entrada.id)
  expect(guardado.observacion).toBe(marca)
  expect(Math.abs(Date.parse(guardado.fechaUtc) - Date.now())).toBeLessThan(10 * 60_000)

  // La limpieza. En un sistema de stock la historia no se borra: se compensa, como en
  // cualquier libro contable. Lo que queda como estaba es la cantidad, y se comprueba.
  const venta = await registrar(request, cabeceras, item, 'Venta', `${marca} (compensa)`)
  expect(venta.cantidadResultante).toBe(antes)
  expect(await cantidadEnStock(request, cabeceras, item)).toBe(antes)
})
