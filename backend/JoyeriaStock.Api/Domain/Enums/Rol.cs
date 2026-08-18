namespace JoyeriaStock.Api.Domain.Enums;

public enum Rol
{
    /// <summary>Ve y opera todos los locales.</summary>
    Admin = 1,

    /// <summary>Ve y opera únicamente el local que tiene asignado.</summary>
    Vendedor = 2
}
