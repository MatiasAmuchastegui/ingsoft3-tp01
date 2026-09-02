import { Navigate, NavLink, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import CatalogoPage from './pages/CatalogoPage'
import LoginPage from './pages/LoginPage'
import StockPage from './pages/StockPage'

/**
 * Raíz de la aplicación y guardia de acceso.
 *
 * Decide entre tres estados: todavía no sé quién sos (cargando), no hay sesión (sólo existe
 * el login) o hay sesión (existe la aplicación entera). Al no registrar siquiera las rutas
 * privadas cuando no hay usuario, no hace falta proteger cada pantalla por separado: escribir
 * `/catalogo` a mano en la barra del navegador no lleva a ningún lado.
 */
export default function App() {
  const { usuario, cargando, logout } = useAuth()

  // Mientras se valida el token guardado no se decide nada: si no, se ve un
  // parpadeo del login en cada recarga de página.
  if (cargando) {
    return <div className="pantalla-centrada">Cargando…</div>
  }

  // Sin sesión sólo existe el login, y cualquier otra ruta redirige ahí.
  if (!usuario) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  return (
    <div className="layout">
      <header className="barra">
        <div className="barra-marca">
          <strong>Joyería</strong>
          <span className="barra-subtitulo">Gestión de stock</span>
        </div>

        <nav className="barra-nav">
          <NavLink to="/stock">Stock</NavLink>
          <NavLink to="/catalogo">Catálogo</NavLink>
        </nav>

        <div className="barra-usuario">
          <span>
            {usuario.nombre} · <span className="etiqueta">{usuario.rol}</span>
            {usuario.localNombre && <> · {usuario.localNombre}</>}
          </span>
          <button type="button" className="boton-plano" onClick={logout}>
            Salir
          </button>
        </div>
      </header>

      <main className="contenido">
        <Routes>
          <Route path="/stock" element={<StockPage />} />
          <Route path="/catalogo" element={<CatalogoPage />} />
          <Route path="*" element={<Navigate to="/stock" replace />} />
        </Routes>
      </main>
    </div>
  )
}
