import { expect, test, type Locator, type Page } from '@playwright/test'
import { credenciales, prefijoUnico } from './comun'

/**
 * End-to-end: la app como la usa una persona, con un navegador de verdad, contra el entorno
 * desplegado (E2E_BASE_URL apunta al front). Nada de dobles ni de respuestas inventadas: si
 * el navegador no le pega a la api de verdad, no es end-to-end.
 *
 * Se busca como busca una persona —el texto de una etiqueta, el rol de un control, la fila
 * de una tabla—, no por clases de CSS, que cambian con el diseño.
 */

const { email, password } = credenciales()

async function ingresar(page: Page) {
  await page.goto('/')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Contraseña').fill(password)
  await page.getByRole('button', { name: 'Ingresar' }).click()
  await expect(page.getByRole('heading', { name: 'Stock por local' })).toBeVisible()
}

async function irACategorias(page: Page) {
  await page.getByRole('link', { name: 'Catálogo' }).click()
  await page.getByRole('tab', { name: /^Categorías/ }).click()
  // La lista terminó de llegar cuando aparecen sus botones: mientras carga hay una sola fila
  // que dice «Cargando…».
  await expect(page.getByRole('button', { name: 'Eliminar' }).first()).toBeVisible()
}

async function registrarMovimiento(page: Page, fila: Locator, tipo: 'Entrada' | 'Venta', observacion: string) {
  await fila.getByRole('button', { name: 'Movimiento' }).click()
  const formulario = page.getByRole('dialog', { name: 'Registrar movimiento' })
  await formulario.getByLabel('Tipo de movimiento').selectOption(tipo)
  await formulario.getByLabel('Cantidad').fill('1')
  await formulario.getByLabel('Observación (opcional)').fill(observacion)
  await formulario.getByRole('button', { name: 'Registrar' }).click()
  await expect(formulario).toBeHidden()
}

test('crear una categoría la muestra en la lista, y eliminarla la saca', async ({ page }) => {
  await ingresar(page)
  await irACategorias(page)

  const nombre = `e2e ${Date.now()}` // único: no choca con otra corrida
  await page.getByRole('button', { name: 'Nueva categoría' }).click()
  const formulario = page.getByRole('dialog', { name: 'Nueva categoría' })
  await formulario.getByLabel('Nombre').fill(nombre)
  await formulario.getByLabel('Prefijo de código').fill(prefijoUnico())
  await formulario.getByRole('button', { name: 'Guardar' }).click()

  const fila = page.getByRole('row', { name: nombre })
  await expect(fila).toBeVisible() // el dato que ESTE test creó, sin sleeps

  // El «¿Eliminar la categoría…?» es un diálogo del navegador, no de la app. Playwright los
  // rechaza solo si nadie le dice otra cosa, y entonces no se borraría nada.
  page.once('dialog', (dialogo) => dialogo.accept())
  await fila.getByRole('button', { name: 'Eliminar' }).click()
  await expect(fila).toHaveCount(0) // y el borrado, comprobado
})

test('un prefijo que ya usa otra categoría muestra el error y no crea nada', async ({ page }) => {
  await ingresar(page)
  await irACategorias(page)

  // El prefijo de una categoría que ya existe, leído de la pantalla como lo leería una
  // persona: la columna Prefijo muestra el primer código de la categoría, «ANI-0001».
  const primera = page.getByRole('row').nth(1)
  const existente = (await primera.getByRole('cell').first().innerText()).trim()
  const prefijo = (await primera.getByRole('cell').nth(1).innerText()).split('-')[0].trim()

  const nombre = `e2e ${Date.now()}`
  await page.getByRole('button', { name: 'Nueva categoría' }).click()
  const formulario = page.getByRole('dialog', { name: 'Nueva categoría' })
  await formulario.getByLabel('Nombre').fill(nombre)
  await formulario.getByLabel('Prefijo de código').fill(prefijo)
  await formulario.getByRole('button', { name: 'Guardar' }).click()

  // El usuario ve el error, y el error habla de SU dato: el prefijo que eligió.
  await expect(formulario.getByRole('alert')).toContainText(`'${prefijo}' ya lo usa otra categoría`)

  // Y no se creó nada. Se recarga la página para preguntarle al servidor, no a lo que quedó
  // en pantalla. La lista llegó cuando aparece la categoría de antes; recién ahí vale
  // comprobar que la nueva no está.
  await formulario.getByRole('button', { name: 'Cancelar' }).click()
  await page.reload()
  await page.getByRole('tab', { name: /^Categorías/ }).click()
  await expect(page.getByRole('cell', { name: existente, exact: true })).toBeVisible()
  await expect(page.getByRole('row', { name: nombre })).toHaveCount(0)
})

test('una entrada suma al stock, y una venta lo deja como estaba', async ({ page }) => {
  await ingresar(page)

  // Lo que se hace todos los días en el mostrador: elegir el local, buscar la pieza y
  // registrar lo que pasó con ella.
  // El select está ADENTRO de su <label>, así que el texto de la etiqueta incluye las
  // opciones. Por su rol y su nombre accesible —«Local»— se lo encuentra igual que lo
  // anuncia un lector de pantalla.
  //
  // Elegir el local dispara otro pedido al servidor, y hasta que la respuesta se dibuja la
  // tabla muestra lo de antes o una fila «Cargando…». Leer en ese momento es leer otra cosa:
  // contra un servidor lento, el test llegó a tomar «Cargando…» como si fuera un SKU. Por eso
  // se espera ESE pedido, y después a que la tabla deje de estar cargando.
  const filtrado = page.waitForResponse((r) => r.url().includes('/api/stock') && r.url().includes('localId='))
  await page.getByRole('combobox', { name: 'Local' }).selectOption({ index: 1 })
  expect((await filtrado).ok(), 'el stock del local llegó bien').toBe(true)
  const stock = page.getByRole('table').first()
  await expect(stock.getByRole('row', { name: 'Cargando…' })).toHaveCount(0)
  await expect(stock.getByRole('button', { name: 'Movimiento' }).first()).toBeVisible()

  // textContent y no innerText: innerText devuelve el texto como se VE, y el CSS pone los
  // encabezados en mayúsculas.
  const columnas = await stock.getByRole('columnheader').allTextContents()
  const columnaCantidad = columnas.findIndex((c) => c.trim() === 'Cantidad')
  expect(columnaCantidad, 'la tabla de stock tiene una columna Cantidad').toBeGreaterThanOrEqual(0)
  const sku = (await stock.getByRole('row').nth(1).getByRole('cell').first().innerText()).trim()

  await page.getByRole('searchbox', { name: 'Buscar' }).fill(sku)
  const fila = stock.getByRole('row', { name: sku })
  await expect(fila).toHaveCount(1)
  const cantidad = fila.getByRole('cell').nth(columnaCantidad)
  const antes = parseInt((await cantidad.textContent()) ?? '', 10)

  // La celda puede traer pegada la pastilla «bajo»: por eso el número se compara sólo hasta
  // donde dejan de haber dígitos.
  await registrarMovimiento(page, fila, 'Entrada', `e2e ${Date.now()}`)
  await expect(cantidad).toHaveText(new RegExp(`^${antes + 1}(?!\d)`)) // una más, a la vista

  // La limpieza: el stock no se borra, se compensa. Y se comprueba que quedó como estaba.
  await registrarMovimiento(page, fila, 'Venta', `e2e ${Date.now()} (compensa)`)
  await expect(cantidad).toHaveText(new RegExp(`^${antes}(?!\d)`))
})
