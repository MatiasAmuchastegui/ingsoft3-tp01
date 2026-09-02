import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api/client'
import type { StockItem, TipoMovimiento } from '../api/tipos'
import Modal from '../components/Modal'
import { pesos } from '../formato'

interface Props {
  item: StockItem
  onCerrar: () => void
  onRegistrado: () => void
}

/**
 * Los tres tipos que una persona puede registrar a mano.
 *
 * Los otros dos del enum del backend —`TransferenciaSalida` y `TransferenciaEntrada`— no
 * están acá a propósito: no se crean sueltos nunca. Sólo nacen de a pares desde una
 * transferencia, y el backend rechaza cualquier intento de crearlos por este camino.
 *
 * "Salida" y "Venta" descuentan las dos, pero se distinguen porque no son lo mismo: una venta
 * guarda precio y total, y un ajuste por rotura no debería aparecer nunca en un informe de
 * ventas.
 */
const TIPOS: { valor: TipoMovimiento; etiqueta: string }[] = [
  { valor: 'Entrada', etiqueta: 'Entrada (ingreso de mercadería)' },
  { valor: 'Venta', etiqueta: 'Venta' },
  { valor: 'Salida', etiqueta: 'Salida (rotura, ajuste, devolución)' },
]

/**
 * Registra un movimiento de stock sobre un producto en un local.
 *
 * Se abre desde una fila de la pantalla de Stock, así que el producto y el local ya vienen
 * decididos: acá sólo se elige qué pasó y con cuántas unidades.
 */
export default function ModalMovimiento({ item, onCerrar, onRegistrado }: Props) {
  // Arranca en "Venta" porque es lo que más se hace en el mostrador.
  const [tipo, setTipo] = useState<TipoMovimiento>('Venta')
  const [cantidad, setCantidad] = useState(1)
  const [observacion, setObservacion] = useState('')
  const [error, setError] = useState<string | null>(null)
  // Bloquea el botón mientras el pedido viaja, para que dos clics no registren dos veces.
  const [enviando, setEnviando] = useState(false)

  // Entrada suma; venta y salida restan. El signo lo decide el tipo, por eso la cantidad que
  // se manda al backend es siempre positiva.
  const esEgreso = tipo !== 'Entrada'
  // Cómo quedaría el stock si se confirma. Se muestra antes de guardar para que quien carga
  // vea el resultado y pueda frenar si no es el que esperaba.
  const cantidadResultante = item.cantidad + (tipo === 'Entrada' ? cantidad : -cantidad)

  // Validación en el cliente que refleja la regla 2 del backend. El backend igual la vuelve
  // a verificar: esto es comodidad para quien usa la pantalla, no la garantía.
  const excedeStock = esEgreso && cantidad > item.cantidad
  const formularioValido = cantidad >= 1 && Number.isInteger(cantidad) && !excedeStock

  const totalVenta = item.precioBase * cantidad

  async function manejarSubmit(evento: FormEvent) {
    evento.preventDefault()
    if (!formularioValido || enviando) return

    setEnviando(true)
    setError(null)
    try {
      await api.movimientos.registrar({
        tipo,
        productoId: item.productoId,
        localId: item.localId,
        cantidad,
        observacion: observacion.trim() || null,
      })
      onRegistrado()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo registrar el movimiento.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Modal titulo="Registrar movimiento" onCerrar={onCerrar}>
      <form onSubmit={manejarSubmit}>
        <div className="resumen">
          <div>
            <code>{item.sku}</code> <strong>{item.productoNombre}</strong>
          </div>
          <div className="ayuda">
            {item.localNombre} · stock actual: <strong>{item.cantidad}</strong>
          </div>
        </div>

        <label htmlFor="tipo">Tipo de movimiento</label>
        <select id="tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoMovimiento)}>
          {TIPOS.map((t) => (
            <option key={t.valor} value={t.valor}>
              {t.etiqueta}
            </option>
          ))}
        </select>

        <label htmlFor="cantidad">Cantidad</label>
        <input
          id="cantidad"
          type="number"
          min={1}
          step={1}
          value={cantidad}
          onChange={(e) => setCantidad(Number(e.target.value))}
        />

        {excedeStock && (
          <p className="mensaje-error" role="alert">
            No podés retirar {cantidad} unidades: en {item.localNombre} hay {item.cantidad}.
          </p>
        )}

        {!excedeStock && (
          <p className="ayuda">
            Stock después del movimiento: <strong>{cantidadResultante}</strong>
          </p>
        )}

        {tipo === 'Venta' && cantidad >= 1 && (
          <div className="recuadro">
            <div>
              Precio unitario: <strong>{pesos(item.precioBase)}</strong>
            </div>
            <div>
              Total: <strong>{pesos(totalVenta)}</strong>
            </div>
            <p className="ayuda">
              El precio queda congelado en el movimiento: si mañana cambia el del producto,
              esta venta sigue diciendo lo que se cobró.
            </p>
          </div>
        )}

        <label htmlFor="observacion">Observación (opcional)</label>
        <input
          id="observacion"
          type="text"
          maxLength={300}
          value={observacion}
          onChange={(e) => setObservacion(e.target.value)}
          placeholder="Ej. venta mostrador, ajuste de inventario"
        />

        {error && (
          <p className="mensaje-error" role="alert">
            {error}
          </p>
        )}

        <div className="acciones">
          <button type="button" className="boton-plano" onClick={onCerrar}>
            Cancelar
          </button>
          <button type="submit" disabled={!formularioValido || enviando}>
            {enviando ? 'Registrando…' : 'Registrar'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
