namespace JoyeriaStock.Api.Infrastructure.Auth;

/// <summary>
/// Configuración del JWT. Se llena desde la sección "Jwt" de appsettings y se puede
/// sobreescribir por variable de entorno (Jwt__Key, Jwt__Issuer, ...) sin tocar código.
/// </summary>
public class JwtOptions
{
    public const string SeccionConfig = "Jwt";

    /// <summary>Clave de firma HMAC-SHA256. Mínimo 32 caracteres. En producción va por variable de entorno.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "JoyeriaStock";
    public string Audience { get; set; } = "JoyeriaStock";
    public int MinutosDeVida { get; set; } = 480;
}
