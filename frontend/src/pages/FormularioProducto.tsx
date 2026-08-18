import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api/client'
import type { Categoria, GuardarProducto, Producto } from '../api/tipos'
import Modal from '../components/Modal'

interface Props {
  /** undefined = crear uno nuevo; objeto = editar el existente. */
  producto: Producto | undefined
  categorias: Categoria[]
  onCerrar: () => void
  onGuardado: () => void
}

export default function FormularioProducto({ producto, categorias, onCerrar, onGuardado }: Props) {
  const [datos, setDatos] = useState<GuardarProducto>({
    nombre: producto?.nombre ?? '',
    categoriaId: producto?.categoriaId ?? categorias[0]?.id ?? 0,
    codigoLinea: producto?.codigoLinea ?? null,
    precioBase: producto?.precioBase ?? 0,
    umbralStockBajo: producto?.umbralStockBajo ?? 0,
  })
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [proximoSku, setProximoSku] = useState<string | null>(null)

  function actualizar<K extends keyof GuardarProducto>(campo: K, valor: GuardarProducto[K]) {
    setDatos((previo) => ({ ...previo, [campo]: valor }))
  }

  // El código lo arma el backend (prefijo de la categoría + línea + correlativo). Se lo
  // pedimos para mostrarlo, en lugar de reconstruirlo acá: si se duplicara la lógica,
  // la vista previa terminaría mostrando un código distinto del que se guarda.
  useEffect(() => {
    if (producto || datos.categoriaId <= 0) return

    let cancelado = false
    const temporizador = setTimeout(() => {
      api.productos
        .proximoSku(datos.categoriaId, datos.codigoLinea)
        .then((r) => {
          if (!cancelado) setProximoSku(r.sku)
        })
        .catch(() => {
          if (!cancelado) setProximoSku(null)
        })
    }, 250)

    return () => {
      cancelado = true
      clearTimeout(temporizador)
    }
  }, [producto, datos.categoriaId, datos.codigoLinea])

  // Mismas validaciones que ProductoService.Validar en el backend. Duplicarlas acá es
  // deliberado: el cliente da el aviso inmediato, el servidor es el que garantiza.
  const formularioValido =
    datos.nombre.trim().length > 0 &&
    datos.categoriaId > 0 &&
    datos.precioBase > 0 &&
    datos.umbralStockBajo >= 0

  async function manejarSubmit(evento: FormEvent) {
    evento.preventDefault()
    if (!formularioValido || enviando) return

    setEnviando(true)
    setError(null)
    try {
      const carga: GuardarProducto = { ...datos, nombre: datos.nombre.trim() }
      if (producto) {
        await api.productos.actualizar(producto.id, carga)
      } else {
        await api.productos.crear(carga)
      }
      onGuardado()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar el producto.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Modal titulo={producto ? 'Editar producto' : 'Nuevo producto'} onCerrar={onCerrar}>
      <form onSubmit={manejarSubmit}>
        <div className="grilla-2">
          <div>
            <label htmlFor="categoria">Categoría</label>
            <select
              id="categoria"
              value={datos.categoriaId}
              onChange={(e) => actualizar('categoriaId', Number(e.target.value))}
              // La categoría define el prefijo del código, y el código no se puede
              // reescribir después: por eso al editar queda fija.
              disabled={!!producto}
            >
              {categorias.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.nombre} ({c.prefijoSku})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="linea">Código de línea o marca (opcional)</label>
            <input
              id="linea"
              type="text"
              maxLength={6}
              value={datos.codigoLinea ?? ''}
              onChange={(e) =>
                actualizar(
                  'codigoLinea',
                  e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6) || null,
                )
              }
              placeholder="CT"
              disabled={!!producto}
              style={{ textTransform: 'uppercase', letterSpacing: '0.1em' }}
            />
            <p className="ayuda">Ej. CT para Citizen. Se suma al prefijo de la categoría.</p>
          </div>
        </div>

        {/* El SKU lo pone el sistema: se muestra, no se edita. */}
        <div className="recuadro">
          {producto ? (
            <>
              <div>
                Código: <code>{producto.sku}</code>
              </div>
              <p className="ayuda">
                No se puede cambiar: está impreso en la etiqueta de la pieza y aparece en todo
                su historial de movimientos.
              </p>
            </>
          ) : (
            <>
              <div>
                Código que le va a tocar:{' '}
                {proximoSku ? <code>{proximoSku}</code> : <span className="ayuda">calculando…</span>}
              </div>
              <p className="ayuda">
                Lo genera el sistema y no se repite. El número es correlativo dentro de cada
                serie.
              </p>
            </>
          )}
        </div>

        <label htmlFor="nombre">Nombre</label>
        <input
          id="nombre"
          type="text"
          maxLength={150}
          value={datos.nombre}
          onChange={(e) => actualizar('nombre', e.target.value)}
          placeholder="Anillo de plata 925 con esmeralda"
        />

        <div className="grilla-2">
          <div>
            <label htmlFor="precio">Precio base</label>
            <input
              id="precio"
              type="number"
              min={0}
              step="0.01"
              value={datos.precioBase}
              onChange={(e) => actualizar('precioBase', Number(e.target.value))}
            />
          </div>

          <div>
            <label htmlFor="umbral">Avisar cuando queden menos de</label>
            <input
              id="umbral"
              type="number"
              min={0}
              step={1}
              value={datos.umbralStockBajo}
              onChange={(e) => actualizar('umbralStockBajo', Number(e.target.value))}
            />
            <p className="ayuda">
              Sólo para el aviso de stock bajo. <strong>No es la cantidad en stock.</strong>
            </p>
          </div>
        </div>

        {/* El catálogo es común a los tres locales; las existencias son de cada local.
            Sin esta aclaración no se entiende por qué acá no se elige local ni cantidad. */}
        {!producto && (
          <p className="ayuda recuadro">
            El producto queda dado de alta en los <strong>tres locales con cantidad 0</strong>.
            Para cargarle unidades, andá a <strong>Stock</strong>, buscalo y registrá una
            <strong> Entrada</strong> en el local que corresponda.
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
