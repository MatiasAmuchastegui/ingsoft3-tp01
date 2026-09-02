import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, borrarToken, guardarToken, leerToken, registrarManejador401 } from '../api/client'
import type { Usuario } from '../api/tipos'

interface EstadoAuth {
  usuario: Usuario | null
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
  useEffect(() => {
    if (leerToken() === null) return

    let cancelado = false
    api
      .perfil()
      .then((perfil) => {
        if (!cancelado) setUsuario(perfil)
      })
      .catch(() => {
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
