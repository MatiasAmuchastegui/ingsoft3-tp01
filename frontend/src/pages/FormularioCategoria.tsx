import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api/client'
import type { Categoria } from '../api/tipos'
import Modal from '../components/Modal'

interface Props {
  /** undefined = crear una nueva; objeto = editar la existente. */
  categoria: Categoria | undefined
  onCerrar: () => void
  onGuardado: () => void
}

export default function FormularioCategoria({ categoria, onCerrar, onGuardado }: Props) {
  const [nombre, setNombre] = useState(categoria?.nombre ?? '')
  const [prefijoSku, setPrefijoSku] = useState(categoria?.prefijoSku ?? '')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  // El prefijo se normaliza mientras se escribe: mayúsculas y sólo letras o números.
  function cambiarPrefijo(valor: string) {
    setPrefijoSku(valor.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6))
  }

  // El prefijo no se puede cambiar si ya hay productos con códigos emitidos.
  const prefijoBloqueado = (categoria?.cantidadProductos ?? 0) > 0

  const formularioValido =
    nombre.trim().length > 0 && nombre.trim().length <= 80 && prefijoSku.length >= 2

  async function manejarSubmit(evento: FormEvent) {
    evento.preventDefault()
    if (!formularioValido || enviando) return

    setEnviando(true)
    setError(null)
    try {
      const datos = { nombre: nombre.trim(), prefijoSku }
      if (categoria) {
        await api.categorias.actualizar(categoria.id, datos)
      } else {
        await api.categorias.crear(datos)
      }
      onGuardado()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la categoría.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Modal titulo={categoria ? 'Editar categoría' : 'Nueva categoría'} onCerrar={onCerrar}>
      <form onSubmit={manejarSubmit}>
        <label htmlFor="nombre">Nombre</label>
        <input
          id="nombre"
          type="text"
          maxLength={80}
          value={nombre}
          onChange={(e) => setNombre(e.target.value)}
          placeholder="Ej. Relojes"
          autoFocus
        />

        <label htmlFor="prefijo">Prefijo de código</label>
        <input
          id="prefijo"
          type="text"
          maxLength={6}
          value={prefijoSku}
          onChange={(e) => cambiarPrefijo(e.target.value)}
          placeholder="REL"
          disabled={prefijoBloqueado}
          style={{ textTransform: 'uppercase', letterSpacing: '0.1em' }}
        />

        {prefijoBloqueado ? (
          <p className="ayuda">
            No se puede cambiar: ya hay {categoria?.cantidadProductos} producto(s) con códigos{' '}
            <code>{categoria?.prefijoSku}-…</code> emitidos, y esos códigos están impresos en
            las piezas.
          </p>
        ) : (
          <p className="ayuda">
            De 2 a 6 letras o números. Con esto arrancan todos los códigos de la categoría:{' '}
            <code>{prefijoSku || 'REL'}-0001</code>, <code>{prefijoSku || 'REL'}-0002</code>…
            El número lo pone el sistema.
          </p>
        )}

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
            {enviando ? 'Guardando…' : 'Guardar'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
