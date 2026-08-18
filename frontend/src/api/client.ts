import type {
  Categoria,
  CrearMovimiento,
  CrearTransferencia,
  GuardarCategoria,
  GuardarProducto,
  Local,
  LoginResponse,
  Movimiento,
  Producto,
  StockItem,
  Transferencia,
  Usuario,
  VistaPreviaSku,
} from './tipos'

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

const CLAVE_TOKEN = 'joyeria.token'

export function guardarToken(token: string) {
  localStorage.setItem(CLAVE_TOKEN, token)
}

export function leerToken(): string | null {
  return localStorage.getItem(CLAVE_TOKEN)
}

export function borrarToken() {
  localStorage.removeItem(CLAVE_TOKEN)
}

/**
 * Error de API con el status HTTP a la vista, para que las pantallas puedan distinguir
 * "credenciales mal" (401) de "regla de negocio" (409) sin parsear el mensaje.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    mensaje: string,
  ) {
    super(mensaje)
    this.name = 'ApiError'
  }
}

/** Se dispara cuando el token venció o dejó de ser válido, para que la app vuelva al login. */
type ManejadorNoAutorizado = () => void
let alRecibir401: ManejadorNoAutorizado | null = null

export function registrarManejador401(handler: ManejadorNoAutorizado) {
  alRecibir401 = handler
}

async function request<T>(ruta: string, init: RequestInit = {}): Promise<T> {
  const token = leerToken()

  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let respuesta: Response
  try {
    respuesta = await fetch(`${BASE_URL}${ruta}`, { ...init, headers })
  } catch {
    // fetch sólo rechaza por problemas de red: el backend no está levantado, o CORS.
    throw new ApiError(0, `No se pudo contactar la API en ${BASE_URL}. ¿Está levantado el backend?`)
  }

  if (respuesta.status === 401) {
    alRecibir401?.()
    throw new ApiError(401, 'Tu sesión venció. Volvé a iniciar sesión.')
  }

  if (respuesta.status === 204) {
    return undefined as T
  }

  if (!respuesta.ok) {
    throw new ApiError(respuesta.status, await leerMensajeDeError(respuesta))
  }

  return (await respuesta.json()) as T
}

/** El backend devuelve ProblemDetails; el mensaje útil está en `detail`. */
async function leerMensajeDeError(respuesta: Response): Promise<string> {
  try {
    const cuerpo = await respuesta.json()
    return cuerpo.detail || cuerpo.title || `Error ${respuesta.status}`
  } catch {
    return `Error ${respuesta.status}`
  }
}

function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const buscador = new URLSearchParams()
  for (const [clave, valor] of Object.entries(params)) {
    if (valor !== undefined && valor !== null && valor !== '') {
      buscador.set(clave, String(valor))
    }
  }
  const texto = buscador.toString()
  return texto ? `?${texto}` : ''
}

export const api = {
  login: (email: string, password: string) =>
    request<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  perfil: () => request<Usuario>('/api/auth/me'),

  locales: () => request<Local[]>('/api/locales'),

  categorias: {
    listar: () => request<Categoria[]>('/api/categorias'),
    crear: (datos: GuardarCategoria) =>
      request<Categoria>('/api/categorias', { method: 'POST', body: JSON.stringify(datos) }),
    actualizar: (id: number, datos: GuardarCategoria) =>
      request<Categoria>(`/api/categorias/${id}`, { method: 'PUT', body: JSON.stringify(datos) }),
    eliminar: (id: number) => request<void>(`/api/categorias/${id}`, { method: 'DELETE' }),
  },

  productos: {
    listar: (incluirInactivos = false) =>
      request<Producto[]>(`/api/productos${query({ incluirInactivos })}`),
    crear: (datos: GuardarProducto) =>
      request<Producto>('/api/productos', { method: 'POST', body: JSON.stringify(datos) }),
    actualizar: (id: number, datos: GuardarProducto) =>
      request<Producto>(`/api/productos/${id}`, { method: 'PUT', body: JSON.stringify(datos) }),
    desactivar: (id: number) => request<void>(`/api/productos/${id}`, { method: 'DELETE' }),
    reactivar: (id: number) => request<void>(`/api/productos/${id}/reactivar`, { method: 'POST' }),
    /** Qué código le tocaría a un producto nuevo, para mostrarlo antes de guardar. */
    proximoSku: (categoriaId: number, codigoLinea: string | null) =>
      request<VistaPreviaSku>(`/api/productos/proximo-sku${query({ categoriaId, codigoLinea })}`),
  },

  stock: {
    listar: (opciones: { localId?: number | null; busqueda?: string; soloStockBajo?: boolean } = {}) =>
      request<StockItem[]>(`/api/stock${query(opciones)}`),
  },

  movimientos: {
    listar: (opciones: { localId?: number | null; productoId?: number; limite?: number } = {}) =>
      request<Movimiento[]>(`/api/movimientos${query(opciones)}`),
    registrar: (datos: CrearMovimiento) =>
      request<Movimiento>('/api/movimientos', { method: 'POST', body: JSON.stringify(datos) }),

    /** Traslado atómico entre locales: descuenta del origen y suma en el destino a la vez. */
    transferir: (datos: CrearTransferencia) =>
      request<Transferencia>('/api/movimientos/transferencia', {
        method: 'POST',
        body: JSON.stringify(datos),
      }),
  },
}
