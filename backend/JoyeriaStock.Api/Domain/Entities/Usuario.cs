using JoyeriaStock.Api.Domain.Enums;

namespace JoyeriaStock.Api.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }

    /// <summary>Único. Es el identificador con el que se hace login.</summary>
    public string Email { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Hash PBKDF2 generado por PasswordHasher&lt;Usuario&gt;. Nunca la contraseña en claro.</summary>
    public string PasswordHash { get; set; } = null!;

    public Rol Rol { get; set; }

    /// <summary>
    /// Local asignado. Invariante: obligatorio para Vendedor, debe ser nulo para Admin
    /// (un admin no está atado a un local porque los ve todos).
    /// </summary>
    public int? LocalId { get; set; }
    public Local? Local { get; set; }

    public bool Activo { get; set; } = true;
}
