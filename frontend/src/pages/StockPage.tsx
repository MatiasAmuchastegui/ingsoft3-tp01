import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { Local, Movimiento, StockItem, TipoMovimiento } from '../api/tipos'
import { useAuth } from '../auth/AuthContext'
import { fechaHora, pesos } from '../formato'
import ModalMovimiento from './ModalMovimiento'
import ModalTransferencia from './ModalTransferencia'

/**
 * Pantalla principal: las existencias por local y el historial reciente.
 *
 * Es donde se hace el trabajo diario. Muestra cuánto hay de cada producto en cada local, y
 * desde cada fila se registra un movimiento (entrada, salida o venta) o —si sos admin— se
 * traslada mercadería a otro local.
 *
 * Debajo van los últimos 15 movimientos. Están a la vista a propósito: después de registrar
 * algo, la confirmación de que quedó bien es verlo aparecer ahí, sin tener que buscarlo.
 */
export default function StockPage() {
  const { usuario, esAdmin } = useAuth()

  const [locales, setLocales] = useState<Local[]>([])
  // Un vendedor arranca fijado en su local; un admin arranca viendo todos (null).
  // Esto es sólo el valor inicial del filtro: al vendedor la API le rechaza cualquier otro
  // local aunque manipule el pedido, así que la restricción real no está acá.
  const [localId, setLocalId] = useState<number | null>(esAdmin ? null : usuario?.localId ?? null)
  const [busqueda, setBusqueda] = useState('')
  const [soloStockBajo, setSoloStockBajo] = useState(false)

  const [items, setItems] = useState<StockItem[]>([])
  const [movimientos, setMovimientos] = useState<Movimiento[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Qué fila tiene abierto un modal. Guardar el item entero (y no sólo su id) le ahorra al
  // modal tener que volver a buscarlo: ya recibe el nombre, el local y la cantidad actual.
  const [itemSeleccionado, setItemSeleccionado] = useState<StockItem | null>(null)
  const [itemATransferir, setItemATransferir] = useState<StockItem | null>(null)

  // Los locales se piden una sola vez: no cambian mientras la pantalla está abierta.
  useEffect(() => {
    api.locales().then(setLocales).catch(() => setLocales([]))
  }, [])

  /**
   * Vuelve a pedir stock y movimientos con los filtros actuales.
   *
   * Los dos pedidos van en paralelo con `Promise.all` porque son independientes: encadenarlos
   * duplicaría la espera sin ninguna ventaja.
   *
   * Después de cada operación se recarga desde el servidor en lugar de actualizar la tabla a
   * mano con lo que se acaba de registrar. Es más pedidos, pero lo que se ve es siempre lo
   * que la base dice, no lo que el navegador cree — y en un sistema donde dos personas pueden
   * estar cargando al mismo tiempo, esa diferencia importa.
   */
  const recargar = useCallback(async () => {
    setCargando(true)
    setError(null)
    try {
      const [stock, ultimos] = await Promise.all([
        api.stock.listar({ localId, busqueda, soloStockBajo }),
        api.movimientos.listar({ localId, limite: 15 }),
      ])
      setItems(stock)
      setMovimientos(ultimos)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cargar el stock.')
    } finally {
      setCargando(false)
    }
  }, [localId, busqueda, soloStockBajo])

  // Debounce del buscador: sin esto se dispara un request por cada tecla.
  // El `return` cancela el temporizador anterior, así que mientras se escriba rápido no sale
  // ningún pedido; recién sale 300 ms después de la última tecla. Cambiar de local o tildar
  // el filtro recarga al instante (delay 0) porque ahí no hay tipeo que esperar.
  useEffect(() => {
    const temporizador = setTimeout(recargar, busqueda ? 300 : 0)
    return () => clearTimeout(temporizador)
  }, [recargar, busqueda])

  // Cuántos productos están por debajo de su umbral, para el contador del filtro.
  // "Bajo" no es un número fijo: cada producto trae el suyo, porque no es lo mismo quedarse
  // con dos alianzas que con dos relojes de vitrina.
  const cantidadStockBajo = items.filter((i) => i.stockBajo).length

  // Traducción de los tipos internos a lo que ve la usuaria. Los nombres del enum del backend
  // (`TransferenciaSalida`) no se muestran nunca: en pantalla son flechas, que se entienden
  // sin explicación. El `Record<TipoMovimiento, …>` obliga a que estén los cinco: si mañana se
  // agrega un tipo al backend y se olvida acá, TypeScript no compila en vez de romper en vivo.
  const ETIQUETAS: Record<TipoMovimiento, { texto: string; clase: string }> = {
    Entrada: { texto: 'Entrada', clase: 'entrada' },
    Salida: { texto: 'Salida', clase: 'salida' },
    Venta: { texto: 'Venta', clase: 'venta' },
    TransferenciaSalida: { texto: 'Traslado ↗', clase: 'traslado' },
    TransferenciaEntrada: { texto: 'Traslado ↙', clase: 'traslado' },
  }

  return (
    <section>
      <div className="encabezado-seccion">
        <div>
          <h1>Stock por local</h1>
          <p className="ayuda">
            {esAdmin
              ? 'Como administradora ves los tres locales. Elegí uno para filtrar.'
              : `Ves y operás únicamente ${usuario?.localNombre}.`}
          </p>
        </div>
        <button type="button" className="boton-plano" onClick={recargar}>
          Actualizar
        </button>
      </div>

      <div className="filtros">
        {esAdmin && (
          <label>
            Local
            <select
              value={localId ?? ''}
              onChange={(e) => setLocalId(e.target.value === '' ? null : Number(e.target.value))}
            >
              <option value="">Todos los locales</option>
              {locales.map((local) => (
                <option key={local.id} value={local.id}>
                  {local.nombre}
                </option>
              ))}
            </select>
          </label>
        )}

        <label>
          Buscar
          <input
            type="search"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Nombre o SKU"
          />
        </label>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={soloStockBajo}
            onChange={(e) => setSoloStockBajo(e.target.checked)}
          />
          Sólo stock bajo
          {cantidadStockBajo > 0 && !soloStockBajo && (
            <span className="pastilla pastilla-alerta">{cantidadStockBajo}</span>
          )}
        </label>
      </div>

      {error && (
        <p className="mensaje-error" role="alert">
          {error}
        </p>
      )}

      <div className="tabla-scroll">
        <table>
          <thead>
            <tr>
              <th>SKU</th>
              <th>Producto</th>
              <th>Categoría</th>
              {localId === null && <th>Local</th>}
              <th className="numero">Cantidad</th>
              <th className="numero">Precio</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {cargando && (
              <tr>
                <td colSpan={7}>Cargando…</td>
              </tr>
            )}

            {!cargando && items.length === 0 && (
              <tr>
                <td colSpan={7}>No hay stock que coincida con el filtro.</td>
              </tr>
            )}

            {!cargando &&
              items.map((item) => (
                <tr key={`${item.productoId}-${item.localId}`} className={item.stockBajo ? 'fila-alerta' : undefined}>
                  <td>
                    <code>{item.sku}</code>
                  </td>
                  <td>{item.productoNombre}</td>
                  <td>{item.categoriaNombre}</td>
                  {localId === null && <td>{item.localNombre}</td>}
                  <td className="numero">
                    {item.cantidad}
                    {item.stockBajo && (
                      <span className="pastilla pastilla-alerta" title={`Umbral: ${item.umbralStockBajo}`}>
                        bajo
                      </span>
                    )}
                  </td>
                  <td className="numero">{pesos(item.precioBase)}</td>
                  <td className="acciones-celda">
                    <button type="button" onClick={() => setItemSeleccionado(item)}>
                      Movimiento
                    </button>
                    {/* Una transferencia toca dos locales, así que sólo la puede hacer un
                        admin. Y sin unidades no hay nada que trasladar. */}
                    {esAdmin && (
                      <button
                        type="button"
                        className="boton-plano"
                        onClick={() => setItemATransferir(item)}
                        disabled={item.cantidad === 0}
                        title={
                          item.cantidad === 0
                            ? 'No hay unidades en este local para trasladar'
                            : `Trasladar desde ${item.localNombre} a otro local`
                        }
                      >
                        Transferir
                      </button>
                    )}
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      <h2 className="titulo-secundario">Últimos movimientos</h2>
      <div className="tabla-scroll">
        <table>
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Tipo</th>
              <th>Producto</th>
              <th>Local</th>
              <th className="numero">Cant.</th>
              <th className="numero">Total</th>
              <th>Usuario</th>
            </tr>
          </thead>
          <tbody>
            {movimientos.length === 0 && (
              <tr>
                <td colSpan={7}>Todavía no hay movimientos registrados.</td>
              </tr>
            )}
            {movimientos.map((m) => (
              <tr key={m.id}>
                <td>{fechaHora(m.fechaUtc)}</td>
                <td>
                  <span
                    className={`pastilla pastilla-${ETIQUETAS[m.tipo].clase}`}
                    title={m.observacion ?? undefined}
                  >
                    {ETIQUETAS[m.tipo].texto}
                  </span>
                </td>
                <td>
                  <code>{m.sku}</code> {m.productoNombre}
                </td>
                <td>{m.localNombre}</td>
                <td className="numero">{m.cantidad}</td>
                <td className="numero">{pesos(m.total)}</td>
                <td>{m.usuarioNombre}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {itemSeleccionado && (
        <ModalMovimiento
          item={itemSeleccionado}
          onCerrar={() => setItemSeleccionado(null)}
          onRegistrado={() => {
            setItemSeleccionado(null)
            void recargar()
          }}
        />
      )}

      {itemATransferir && (
        <ModalTransferencia
          item={itemATransferir}
          locales={locales}
          onCerrar={() => setItemATransferir(null)}
          onTransferido={() => {
            setItemATransferir(null)
            void recargar()
          }}
        />
      )}
    </section>
  )
}
