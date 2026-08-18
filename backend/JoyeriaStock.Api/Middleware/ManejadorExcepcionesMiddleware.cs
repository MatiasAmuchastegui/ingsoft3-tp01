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
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (estado, titulo) = Traducir(ex);

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

            var problema = new ProblemDetails
            {
                Status = (int)estado,
                Title = titulo,
                // En 500 no se filtra el mensaje interno al cliente.
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
