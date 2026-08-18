namespace JoyeriaStock.Api.Domain.Entities;

/// <summary>
/// Existencia de un producto en un local concreto. El stock es SIEMPRE por local, nunca global.
/// </summary>
/// <remarks>
/// La clave primaria es compuesta (ProductoId, LocalId): la base garantiza que no puedan existir
/// dos filas para el mismo par. Esta tabla es la "foto" del stock; la verdad histórica está en
/// <see cref="Movimiento"/>, y esta cantidad sólo se modifica desde MovimientoService dentro de
/// la misma transacción que inserta el movimiento.
/// </remarks>
public class Stock
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int LocalId { get; set; }
    public Local Local { get; set; } = null!;

    /// <summary>Nunca puede quedar negativa (regla de negocio 2).</summary>
    public int Cantidad { get; set; }
}
