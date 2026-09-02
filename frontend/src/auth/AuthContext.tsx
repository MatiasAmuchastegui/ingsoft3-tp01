import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, borrarToken, guardarToken, leerToken, registrarManejador401 } from '../api/client'
import type { Usuario } from '../api/tipos'

/**
 * Quién está usando la aplicación, disponible desde cualquier pantalla.
 *
 * Se resuelve con un contexto de React y no pasando el usuario por props, porque lo necesitan
 * componentes de niveles muy distintos: la barra de navegación para el nombre, la pantalla de
 * Stock para saber si mostrar el selector de locales, y los formularios para habilitar o no
 * los botones de edición.
 *
 * Importante: **esto es comodidad de interfaz, no seguridad**. Que un vendedor no vea el
 * botón de transferir no es lo que le impide transferir — eso lo impide el backend, que
 * valida el rol del token en cada llamada. Si alguien llama a la API a mano, la respuesta es
 * la misma. La interfaz esconde lo que no corresponde; el servidor lo prohíbe.
 */
interface EstadoAuth {
  usuario: Usuario | null
  /** True mientras se valida un token guardado; evita el parpadeo del login al recargar. */
  cargando: boolean
  esAdmin: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<EstadoAuth | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  // Arranca cargando sólo si hay un token guardado que haya que validar.
  const [cargando, setCargando] = useState(() => leerToken() !== null)

  const logout = useCallback(() => {
    borrarToken()
    setUsuario(null)
  }, [])

  // Si la API responde 401 en cualquier momento (token vencido), se cierra la sesión.
  useEffect(() => {
    registrarManejador401(logout)
  }, [logout])

  // Al recargar la página el token sigue en localStorage pero el usuario no está en memoria:
  // se lo pide a la API, que además valida que el token siga siendo bueno.
  //
  // Se le pregunta al servidor en vez de leer los datos del propio token porque el token no
  // es confiable como fuente de verdad para la interfaz: lo firmó el servidor, sí, pero su
  // contenido puede haber quedado viejo (le cambiaron el rol, le reasignaron el local). El
  // servidor responde con el estado actual.
  useEffect(() => {
    if (leerToken() === null) return

    // `cancelado` evita actualizar el estado de un componente que ya se desmontó: si el
    // usuario se va de la pantalla antes de que responda la API, la respuesta llega igual y
    // sin esta guarda React avisa que se está escribiendo sobre algo que ya no existe.
    let cancelado = false
    api
      .perfil()
      .then((perfil) => {
        if (!cancelado) setUsuario(perfil)
      })
      .catch(() => {
        // El token estaba pero ya no sirve (venció, o lo firmó otra clave). Se descarta y la
        // app cae al login, en vez de quedar en un limbo con sesión aparente y API que rechaza.
        if (!cancelado) borrarToken()
      })
      .finally(() => {
        if (!cancelado) setCargando(false)
      })

    return () => {
      cancelado = true
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const respuesta = await api.login(email, password)
    guardarToken(respuesta.token)
    setUsuario(respuesta.usuario)
  }, [])

  const valor = useMemo<EstadoAuth>(
    () => ({ usuario, cargando, esAdmin: usuario?.rol === 'Admin', login, logout }),
    [usuario, cargando, login, logout],
  )

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}

export function useAuth(): EstadoAuth {
  const contexto = useContext(AuthContext)
  if (!contexto) throw new Error('useAuth se tiene que usar dentro de <AuthProvider>.')
  return contexto
}
