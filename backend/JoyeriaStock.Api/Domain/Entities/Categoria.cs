namespace JoyeriaStock.Api.Domain.Entities;

/// <summary>Agrupación de productos: anillos, collares, pulseras, aros...</summary>
public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;

    /// <summary>
    /// Letras con las que empiezan los SKU de esta categoría (Relojes → REL).
    /// Es la parte fija del código que el sistema genera para cada producto.
    /// </summary>
    public string PrefijoSku { get; set; } = null!;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
