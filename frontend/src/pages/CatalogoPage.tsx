import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { Categoria, Producto } from '../api/tipos'
import { useAuth } from '../auth/AuthContext'
import { pesos } from '../formato'
import FormularioCategoria from './FormularioCategoria'
import FormularioProducto from './FormularioProducto'

type Pestana = 'productos' | 'categorias'

export default function CatalogoPage() {
  const { esAdmin } = useAuth()

  const [pestana, setPestana] = useState<Pestana>('productos')
  const [categorias, setCategorias] = useState<Categoria[]>([])
  const [productos, setProductos] = useState<Producto[]>([])
  const [incluirInactivos, setIncluirInactivos] = useState(false)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // null = cerrado; undefined = abierto para crear; objeto = abierto para editar.
  const [editandoCategoria, setEditandoCategoria] = useState<Categoria | undefined | null>(null)
  const [editandoProducto, setEditandoProducto] = useState<Producto | undefined | null>(null)

  const recargar = useCallback(async () => {
    setCargando(true)
    setError(null)
    try {
      const [cats, prods] = await Promise.all([
        api.categorias.listar(),
        api.productos.listar(incluirInactivos),
      ])
      setCategorias(cats)
      setProductos(prods)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cargar el catálogo.')
    } finally {
      setCargando(false)
    }
  }, [incluirInactivos])

  useEffect(() => {
    void recargar()
  }, [recargar])

  async function eliminarCategoria(categoria: Categoria) {
    if (!window.confirm(`¿Eliminar la categoría "${categoria.nombre}"?`)) return

    setError(null)
    try {
      await api.categorias.eliminar(categoria.id)
      await recargar()
    } catch (e) {
      // Acá aterriza la regla 3: si la categoría tiene productos, el backend devuelve 409
      // con el detalle, y se muestra tal cual.
      setError(e instanceof Error ? e.message : 'No se pudo eliminar la categoría.')
    }
  }

  async function cambiarEstadoProducto(producto: Producto) {
    setError(null)
    try {
      if (producto.activo) {
        if (!window.confirm(`¿Dar de baja "${producto.nombre}"? Su historial se conserva.`)) return
        await api.productos.desactivar(producto.id)
      } else {
        await api.productos.reactivar(producto.id)
      }
      await recargar()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cambiar el estado del producto.')
    }
  }

  return (
    <section>
      <div className="encabezado-seccion">
        <div>
          <h1>Catálogo</h1>
          <p className="ayuda">
            {esAdmin
              ? 'Productos y categorías son comunes a los tres locales.'
              : 'Sólo lectura: el catálogo lo administra un usuario Admin.'}
          </p>
        </div>
      </div>

      <div className="pestanas" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={pestana === 'productos'}
          className={pestana === 'productos' ? 'activa' : undefined}
          onClick={() => setPestana('productos')}
        >
          Productos ({productos.length})
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={pestana === 'categorias'}
          className={pestana === 'categorias' ? 'activa' : undefined}
          onClick={() => setPestana('categorias')}
        >
          Categorías ({categorias.length})
        </button>
      </div>

      {error && (
        <p className="mensaje-error" role="alert">
          {error}
        </p>
      )}

      {pestana === 'productos' && (
        <>
          <div className="filtros">
            {esAdmin && (
              <button type="button" onClick={() => setEditandoProducto(undefined)}>
                Nuevo producto
              </button>
            )}
            <label className="checkbox">
              <input
                type="checkbox"
                checked={incluirInactivos}
                onChange={(e) => setIncluirInactivos(e.target.checked)}
              />
              Mostrar dados de baja
            </label>
          </div>

          <div className="tabla-scroll">
            <table>
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Nombre</th>
                  <th>Categoría</th>
                  <th className="numero">Precio</th>
                  <th className="numero">Avisar bajo</th>
                  {esAdmin && <th />}
                </tr>
              </thead>
              <tbody>
                {cargando && (
                  <tr>
                    <td colSpan={7}>Cargando…</td>
                  </tr>
                )}
                {!cargando && productos.length === 0 && (
                  <tr>
                    <td colSpan={7}>No hay productos cargados.</td>
                  </tr>
                )}
                {!cargando &&
                  productos.map((p) => (
                    <tr key={p.id} className={p.activo ? undefined : 'fila-inactiva'}>
                      <td>
                        <code>{p.sku}</code>
                      </td>
                      <td>
                        {p.nombre}
                        {!p.activo && <span className="pastilla">baja</span>}
                      </td>
                      <td>{p.categoriaNombre}</td>
                      <td className="numero">{pesos(p.precioBase)}</td>
                      <td className="numero">{p.umbralStockBajo}</td>
                      {esAdmin && (
                        <td className="acciones-celda">
                          <button type="button" className="boton-plano" onClick={() => setEditandoProducto(p)}>
                            Editar
                          </button>
                          <button type="button" className="boton-plano" onClick={() => cambiarEstadoProducto(p)}>
                            {p.activo ? 'Dar de baja' : 'Reactivar'}
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {pestana === 'categorias' && (
        <>
          <div className="filtros">
            {esAdmin && (
              <button type="button" onClick={() => setEditandoCategoria(undefined)}>
                Nueva categoría
              </button>
            )}
          </div>

          <div className="tabla-scroll">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Prefijo</th>
                  <th className="numero">Productos</th>
                  {esAdmin && <th />}
                </tr>
              </thead>
              <tbody>
                {cargando && (
                  <tr>
                    <td colSpan={4}>Cargando…</td>
                  </tr>
                )}
                {!cargando && categorias.length === 0 && (
                  <tr>
                    <td colSpan={4}>No hay categorías cargadas.</td>
                  </tr>
                )}
                {!cargando &&
                  categorias.map((c) => (
                    <tr key={c.id}>
                      <td>{c.nombre}</td>
                      <td>
                        <code>{c.prefijoSku}-0001</code>
                      </td>
                      <td className="numero">{c.cantidadProductos}</td>
                      {esAdmin && (
                        <td className="acciones-celda">
                          <button type="button" className="boton-plano" onClick={() => setEditandoCategoria(c)}>
                            Editar
                          </button>
                          <button
                            type="button"
                            className="boton-plano"
                            onClick={() => eliminarCategoria(c)}
                            // Se deshabilita cuando tiene productos, con el motivo en el title.
                            // El backend igual lo rechaza: esto sólo evita el click inútil.
                            disabled={c.cantidadProductos > 0}
                            title={
                              c.cantidadProductos > 0
                                ? `No se puede eliminar: tiene ${c.cantidadProductos} producto(s) asociado(s).`
                                : undefined
                            }
                          >
                            Eliminar
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {editandoCategoria !== null && (
        <FormularioCategoria
          categoria={editandoCategoria}
          onCerrar={() => setEditandoCategoria(null)}
          onGuardado={() => {
            setEditandoCategoria(null)
            void recargar()
          }}
        />
      )}

      {editandoProducto !== null && (
        <FormularioProducto
          producto={editandoProducto}
          categorias={categorias}
          onCerrar={() => setEditandoProducto(null)}
          onGuardado={() => {
            setEditandoProducto(null)
            void recargar()
          }}
        />
      )}
    </section>
  )
}
