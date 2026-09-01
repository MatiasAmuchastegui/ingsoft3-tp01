using System.Net;
using JoyeriaStock.Api.Application.Services;
using JoyeriaStock.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Middleware;

/// <summary>
/// Traduce las excepciones del dominio a códigos HTTP en un solo lugar.
/// Gracias a esto los controllers no llevan try/catch y los services pueden tirar
/// excepciones con mensajes en castellano sin saber nada de HTTP.
/// </summary>
public class ManejadorExcepcionesMiddleware(RequestDelegate next, ILogger<ManejadorExcepcionesMiddleware> logger)
{
    /// <summary>
    /// Envuelve el resto de la tubería: deja pasar el pedido y atrapa lo que explote más adentro.
    /// </summary>
    /// <remarks>
    /// Se registra primero de todo en <c>Program.cs</c> justamente por eso: lo que se registra
    /// antes es lo que envuelve a todo lo demás.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (estado, titulo) = Traducir(ex);

            // La distinción importa para operar el sistema. Un 409 es el sistema funcionando:
            // alguien intentó algo que las reglas no permiten y se le dijo que no. Un 500 es un
            // defecto, y va con el stack trace completo porque hay que ir a arreglarlo.
            // Si todo se registrara como error, los logs se llenarían de ruido y el error de
            // verdad pasaría desapercibido.
            if (estado == HttpStatusCode.InternalServerError)
                logger.LogError(ex, "Error no controlado procesando {Metodo} {Ruta}", context.Request.Method, context.Request.Path);
            else
                logger.LogInformation("Solicitud rechazada ({Estado}): {Mensaje}", (int)estado, ex.Message);

            if (context.Response.HasStarted)
            {
                // Ya se empezó a escribir la respuesta: no se puede cambiar el status code.
                logger.LogWarning("La respuesta ya había comenzado; no se pudo devolver el error formateado.");
                return;
            }

            // ProblemDetails (RFC 7807) es el formato estándar de error de las APIs HTTP. Se usa
            // en vez de un JSON propio para que el cliente siempre encuentre el mensaje en el
            // mismo campo: el frontend lee `detail` y no tiene que adivinar la forma del error.
            var problema = new ProblemDetails
            {
                Status = (int)estado,
                Title = titulo,
                // En 500 no se filtra el mensaje interno al cliente: un mensaje de excepción
                // puede revelar nombres de tablas, rutas o parte de la consulta que falló. El
                // detalle real queda en el log del servidor, que sí es de quien opera el sistema.
                Detail = estado == HttpStatusCode.InternalServerError
                    ? "Ocurrió un error inesperado. Revisá los logs del servidor."
                    : ex.Message,
                Instance = context.Request.Path
            };

            context.Response.Clear();
            context.Response.StatusCode = problema.Status.Value;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problema);
        }
    }

    /// <summary>
    /// La tabla de equivalencias entre el vocabulario del dominio y el de HTTP.
    /// </summary>
    /// <remarks>
    /// Éste es el único lugar del backend que conoce las dos cosas a la vez. Los servicios
    /// tiran <c>ReglaNegocioException("El stock no puede quedar negativo")</c> sin enterarse
    /// de que existe un 409, y acá se hace la traducción.
    ///
    /// El caso por defecto es 500 a propósito: cualquier excepción que no esté prevista es un
    /// defecto del sistema, no una decisión de negocio, y tiene que verse como tal.
    /// </remarks>
    private static (HttpStatusCode Estado, string Titulo) Traducir(Exception ex) => ex switch
    {
        ReglaNegocioException          => (HttpStatusCode.Conflict, "Regla de negocio no cumplida"),
        ConflictoConcurrenciaException => (HttpStatusCode.Conflict, "Conflicto de concurrencia"),
        RecursoNoEncontradoException   => (HttpStatusCode.NotFound, "Recurso no encontrado"),
        AccesoDenegadoException        => (HttpStatusCode.Forbidden, "Acceso denegado"),
        CredencialesInvalidasException => (HttpStatusCode.Unauthorized, "Credenciales inválidas"),
        _                              => (HttpStatusCode.InternalServerError, "Error interno")
    };
}
