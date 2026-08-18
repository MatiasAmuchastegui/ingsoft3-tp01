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

const TIPOS: { valor: TipoMovimiento; etiqueta: string }[] = [
  { valor: 'Entrada', etiqueta: 'Entrada (ingreso de mercadería)' },
  { valor: 'Venta', etiqueta: 'Venta' },
  { valor: 'Salida', etiqueta: 'Salida (rotura, ajuste, devolución)' },
]

export default function ModalMovimiento({ item, onCerrar, onRegistrado }: Props) {
  const [tipo, setTipo] = useState<TipoMovimiento>('Venta')
  const [cantidad, setCantidad] = useState(1)
  const [observacion, setObservacion] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const esEgreso = tipo !== 'Entrada'
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
