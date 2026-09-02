import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api/client'
import type { Local, StockItem } from '../api/tipos'
import Modal from '../components/Modal'

interface Props {
  /** Fila desde la que se abrió: define el producto y el local de ORIGEN. */
  item: StockItem
  locales: Local[]
  onCerrar: () => void
  onTransferido: () => void
}

export default function ModalTransferencia({ item, locales, onCerrar, onTransferido }: Props) {
  // El destino nunca puede ser el origen, así que ni siquiera se ofrece.
  const destinosPosibles = locales.filter((l) => l.id !== item.localId)

  const [localDestinoId, setLocalDestinoId] = useState(destinosPosibles[0]?.id ?? 0)
  const [cantidad, setCantidad] = useState(1)
  const [observacion, setObservacion] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const excedeStock = cantidad > item.cantidad
  const formularioValido =
    cantidad >= 1 && Number.isInteger(cantidad) && !excedeStock && localDestinoId > 0

  const destino = locales.find((l) => l.id === localDestinoId)

  async function manejarSubmit(evento: FormEvent) {
    evento.preventDefault()
    if (!formularioValido || enviando) return

    setEnviando(true)
    setError(null)
    try {
      await api.movimientos.transferir({
        productoId: item.productoId,
        localOrigenId: item.localId,
        localDestinoId,
        cantidad,
        observacion: observacion.trim() || null,
      })
      onTransferido()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo hacer la transferencia.')
    } finally {
      setEnviando(false)
    }
  }

  if (destinosPosibles.length === 0) {
    return (
      <Modal titulo="Transferir a otro local" onCerrar={onCerrar}>
        <p className="ayuda">
          No hay otro local al que transferir. Hace falta al menos un segundo local.
        </p>
        <div className="acciones">
          <button type="button" className="boton-plano" onClick={onCerrar}>
            Cerrar
          </button>
        </div>
      </Modal>
    )
  }

  return (
    <Modal titulo="Transferir a otro local" onCerrar={onCerrar}>
      <form onSubmit={manejarSubmit}>
        <div className="resumen">
          <div>
            <code>{item.sku}</code> <strong>{item.productoNombre}</strong>
          </div>
          <div className="ayuda">
            Sale de <strong>{item.localNombre}</strong>, donde hay{' '}
            <strong>{item.cantidad}</strong> unidad(es)
          </div>
        </div>

        <label htmlFor="destino">Local de destino</label>
        <select
          id="destino"
          value={localDestinoId}
          onChange={(e) => setLocalDestinoId(Number(e.target.value))}
        >
          {destinosPosibles.map((l) => (
            <option key={l.id} value={l.id}>
              {l.nombre}
            </option>
          ))}
        </select>

        <label htmlFor="cantidad-transf">Cantidad a trasladar</label>
        <input
          id="cantidad-transf"
          type="number"
          min={1}
          max={item.cantidad}
          step={1}
          value={cantidad}
          onChange={(e) => setCantidad(Number(e.target.value))}
        />

        {excedeStock ? (
          <p className="mensaje-error" role="alert">
            No podés trasladar {cantidad}: en {item.localNombre} hay {item.cantidad}.
          </p>
        ) : (
          <div className="recuadro">
            <div>
              {item.localNombre}: <strong>{item.cantidad}</strong> →{' '}
              <strong>{item.cantidad - cantidad}</strong>
            </div>
            <div>
              {destino?.nombre}: suma <strong>{cantidad}</strong>
            </div>
            <p className="ayuda">
              Las dos cosas pasan en una sola operación: o se mueve todo, o no se mueve nada.
            </p>
          </div>
        )}

        <label htmlFor="obs-transf">Observación (opcional)</label>
        <input
          id="obs-transf"
          type="text"
          maxLength={300}
          value={observacion}
          onChange={(e) => setObservacion(e.target.value)}
          placeholder="Ej. pedido de la sucursal, reposición"
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
            {enviando ? 'Transfiriendo…' : 'Transferir'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
