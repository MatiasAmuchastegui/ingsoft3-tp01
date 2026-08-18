using JoyeriaStock.Api.Domain.Entities;

namespace JoyeriaStock.Api.Application.Abstractions;

public interface IGeneradorToken
{
    /// <summary>Devuelve el JWT firmado y el instante UTC en que expira.</summary>
    (string Token, DateTime ExpiraUtc) Generar(Usuario usuario);
}
