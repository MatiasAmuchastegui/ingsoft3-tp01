namespace JoyeriaStock.Api.Infrastructure.Auth;

/// <summary>
/// Nombres de los claims del token, en un solo lugar para no escribirlos a mano dos veces.
/// </summary>
/// <remarks>
/// Se usan nombres cortos y literales ("sub", "role") en lugar de las URIs largas de
/// ClaimTypes. Para eso el handler se configura con MapInboundClaims = false, y
/// TokenValidationParameters declara cuál claim es el nombre y cuál el rol.
/// </remarks>
public static class ClaimsPersonalizados
{
    public const string Sub = "sub";
    public const string Email = "email";
    public const string Nombre = "name";
    public const string Rol = "role";
    public const string LocalId = "local_id";
}
