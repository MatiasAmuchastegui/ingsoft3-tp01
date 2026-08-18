// Espejo de los DTOs del backend (backend/JoyeriaStock.Api/Application/Dtos/Dtos.cs).
// Si cambia un DTO allá, hay que cambiarlo acá. Es el precio de no generar el cliente
// automáticamente desde Swagger; para este tamaño de app conviene la copia a mano.

export type Rol = 'Admin' | 'Vendedor'

export type TipoMovimiento =
  | 'Entrada'
  | 'Salida'
  | 'Venta'
  // Los dos de transferencia no se registran sueltos: los crea de a pares el endpoint
  // de transferencia, en una sola transacción.
  | 'TransferenciaSalida'
  | 'TransferenciaEntrada'

export interface Usuario {
  id: number
  email: string
  nombre: string
  rol: Rol
  localId: number | null
  localNombre: string | null
}

export interface LoginResponse {
  token: string
  expiraUtc: string
  usuario: Usuario
}

export interface Local {
  id: number
  nombre: string
  direccion: string
}

export interface Categoria {
  id: number
  nombre: string
  /** Letras con las que empiezan los SKU de esta categoría (Relojes → REL). */
  prefijoSku: string
  cantidadProductos: number
}

export interface GuardarCategoria {
  nombre: string
  prefijoSku: string
}

export interface Producto {
  id: number
  /** Lo genera el sistema al crear y no cambia nunca más. */
  sku: string
  codigoLinea: string | null
  nombre: string
  categoriaId: number
  categoriaNombre: string
  precioBase: number
  umbralStockBajo: number
  activo: boolean
}

/** Sin `sku`: lo pone el sistema. Lo único que se aporta al código es `codigoLinea`. */
export interface GuardarProducto {
  nombre: string
  categoriaId: number
  codigoLinea: string | null
  precioBase: number
  umbralStockBajo: number
}

export interface VistaPreviaSku {
  sku: string
}

export interface StockItem {
  productoId: number
  sku: string
  productoNombre: string
  categoriaNombre: string
  localId: number
  localNombre: string
  cantidad: number
  umbralStockBajo: number
  stockBajo: boolean
  precioBase: number
}

export interface Movimiento {
  id: number
  tipo: TipoMovimiento
  productoId: number
  sku: string
  productoNombre: string
  localId: number
  localNombre: string
  cantidad: number
  fechaUtc: string
  usuarioNombre: string
  observacion: string | null
  precioUnitarioAplicado: number | null
  total: number | null
  cantidadResultante: number
  /** Compartido por los dos asientos de una misma transferencia. Null en el resto. */
  transferenciaId: string | null
}

export interface CrearTransferencia {
  productoId: number
  localOrigenId: number
  localDestinoId: number
  cantidad: number
  observacion: string | null
}

export interface Transferencia {
  transferenciaId: string
  productoId: number
  sku: string
  productoNombre: string
  cantidad: number
  fechaUtc: string
  localOrigenId: number
  localOrigenNombre: string
  cantidadResultanteOrigen: number
  localDestinoId: number
  localDestinoNombre: string
  cantidadResultanteDestino: number
}

export interface CrearMovimiento {
  tipo: TipoMovimiento
  productoId: number
  localId: number
  cantidad: number
  observacion: string | null
}

