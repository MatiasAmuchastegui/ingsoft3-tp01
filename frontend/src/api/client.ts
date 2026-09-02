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

/**
 * Único punto por el que esta aplicación habla con la API.
 *
 * Todas las pantallas importan `api` de acá y ninguna llama a `fetch` por su cuenta. Eso
 * concentra en un solo lugar tres cosas que si no habría que repetir en cada pantalla:
 * mandar el token, traducir los errores del backend a algo legible, y detectar la sesión
 * vencida.
 */

/**
 * A dónde se le pega a la API.
 *
 * Vacío significa **mismo origen**: el navegador pide `/api/...` a quien le sirvió la página
 * (nginx), y nginx lo reenvía al backend. Ésa es la configuración con la que se compila la
 * imagen de producción, y es la razón por la que el bundle NO lleva ninguna URL adentro: la
 * misma imagen sirve en localhost, en la PC de la joyería o en cualquier IP de la red, sin
 * recompilar.
 *
 * El valor por defecto (`localhost:5080`) es sólo para desarrollo, cuando el frontend corre
 * en el 5173 con Vite y el backend aparte en el 5080. Ahí sí son orígenes distintos y hace
 * falta CORS.
 *
 * Ojo: Vite reemplaza `import.meta.env.VITE_*` en tiempo de COMPILACIÓN, no de ejecución.
 * Cambiar esta variable exige reconstruir la imagen.
 */
const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

// ---------------------------------------------------------------------------
// El token de sesión
// ---------------------------------------------------------------------------
// Se guarda en localStorage y no en memoria para que recargar la página (F5) no te eche.
// Sobrevive a cerrar y reabrir el navegador; lo que lo invalida es que venza el plazo que
// el backend le puso al firmarlo.

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

/**
 * El corazón del cliente: hace la llamada HTTP y normaliza todo lo que puede salir mal.
 *
 * Cada pantalla llama a esto (a través de `api`) y sólo tiene que preocuparse por dos casos:
 * salió bien y tengo el dato, o me tiraron un `ApiError` con un mensaje que puedo mostrar.
 * Todo el resto se resuelve acá.
 */
async function request<T>(ruta: string, init: RequestInit = {}): Promise<T> {
  const token = leerToken()

  // El token viaja en cada pedido. Es la consecuencia de que la API no tenga sesión: cada
  // llamada se autentica sola, el servidor no recuerda nada entre una y otra.
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let respuesta: Response
  try {
    respuesta = await fetch(`${BASE_URL}${ruta}`, { ...init, headers })
  } catch {
    // fetch sólo rechaza por problemas de red: el backend no está levantado, o CORS.
    // Un 404 o un 500 NO caen acá — ésos son respuestas válidas y se revisan más abajo.
    throw new ApiError(0, `No se pudo contactar la API en ${BASE_URL}. ¿Está levantado el backend?`)
  }

  // 401 se trata aparte de los demás errores porque no es un problema de esta pantalla: es
  // que la sesión dejó de servir. Se avisa a quien registró el manejador (el contexto de
  // autenticación) para que limpie el token y devuelva la app al login.
  if (respuesta.status === 401) {
    alRecibir401?.()
    throw new ApiError(401, 'Tu sesión venció. Volvé a iniciar sesión.')
  }

  // 204 No Content: la operación salió bien pero no hay cuerpo que parsear (los DELETE).
  // Intentar leer JSON acá explotaría.
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

/**
 * Arma la query string salteando lo que no tiene valor.
 *
 * Sin este filtro, un filtro vacío viajaría como `?busqueda=` y el backend tendría que
 * distinguir "no filtres" de "filtrá por texto vacío". Descartándolo acá, un parámetro
 * ausente significa siempre lo mismo: no filtres por eso.
 */
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

/**
 * El catálogo de operaciones disponibles, agrupado como los recursos de la API.
 *
 * Está tipado de punta a punta: `request<Producto[]>` hace que TypeScript sepa qué devuelve
 * cada llamada, así que si el backend cambia un DTO y se actualiza `tipos.ts`, el compilador
 * marca todas las pantallas que quedaron desalineadas. Es un error de compilación en vez de
 * un `undefined` en pantalla.
 */
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
