using JoyeriaStock.Api.Domain.Enums;

namespace JoyeriaStock.Api.Domain.Entities;

/// <summary>
/// Libro mayor de existencias: cada variación de stock deja acá su asiento.
/// Es append-only — no se edita ni se borra nunca. Si un movimiento fue un error,
/// se registra el movimiento contrario.
/// </summary>
public class Movimiento
{
    public int Id { get; set; }

    public TipoMovimiento Tipo { get; set; }

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int LocalId { get; set; }
    public Local Local { get; set; } = null!;

    /// <summary>Siempre positiva. El signo lo determina <see cref="Tipo"/>.</summary>
    public int Cantidad { get; set; }

    /// <summary>Siempre en UTC (columna timestamptz).</summary>
    public DateTime FechaUtc { get; set; }

    /// <summary>Quién lo registró. Sin esto no hay auditoría.</summary>
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string? Observacion { get; set; }

    /// <summary>
    /// Sólo en transferencias: identificador compartido por los dos asientos (la salida del
    /// local origen y la entrada al destino). Permite reconstruir el traslado como una sola
    /// operación en el historial, y detectar si alguna vez quedara un asiento huérfano.
    /// </summary>
    public Guid? TransferenciaId { get; set; }

    /// <summary>
    /// Sólo en ventas: precio unitario efectivamente cobrado, ya con el descuento mayorista
    /// aplicado si correspondía. Se persiste para que cambiar el precio del producto no
    /// reescriba la historia de las ventas pasadas.
    /// </summary>
    public decimal? PrecioUnitarioAplicado { get; set; }

    /// <summary>Sólo en ventas: PrecioUnitarioAplicado * Cantidad.</summary>
    public decimal? Total { get; set; }
}
