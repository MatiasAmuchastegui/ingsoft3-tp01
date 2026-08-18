namespace JoyeriaStock.Api.Domain;

/// <summary>
/// Se violó una regla de negocio (SKU duplicado, stock insuficiente, categoría con productos...).
/// El middleware la traduce a HTTP 409 Conflict.
/// </summary>
public class ReglaNegocioException(string message) : Exception(message);

/// <summary>No existe el recurso pedido. Se traduce a HTTP 404.</summary>
public class RecursoNoEncontradoException(string message) : Exception(message);

/// <summary>
/// El usuario está autenticado pero no tiene permiso sobre este recurso
/// (típicamente: un vendedor tocando un local que no es el suyo). Se traduce a HTTP 403.
/// </summary>
public class AccesoDenegadoException(string message) : Exception(message);

/// <summary>
/// Dos operaciones simultáneas intentaron modificar el mismo stock.
/// Se traduce a HTTP 409 para que el cliente reintente.
/// </summary>
public class ConflictoConcurrenciaException(string message) : Exception(message);
