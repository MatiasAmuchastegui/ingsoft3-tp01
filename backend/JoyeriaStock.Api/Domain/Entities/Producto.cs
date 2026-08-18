namespace JoyeriaStock.Api.Domain.Entities;

/// <summary>
/// Un artículo del catálogo. El catálogo es global (compartido por los 3 locales);
/// lo que es por local es el <see cref="Stock"/>.
/// </summary>
public class Producto
{
    public int Id { get; set; }

    /// <summary>
    /// Código interno único en todo el sistema (regla de negocio 1). Lo genera el sistema
    /// al dar de alta el producto, con el formato PREFIJO-NNNN, y NO se puede cambiar
    /// después: va impreso en la etiqueta de la pieza.
    /// </summary>
    public string Sku { get; set; } = null!;

    /// <summary>
    /// Código opcional de línea o marca que se suma al prefijo de la categoría
    /// (Relojes = REL, marca Citizen = CT → los SKU quedan RELCT-0001, RELCT-0002...).
    /// Se guarda para poder mostrar cómo se compuso el código.
    /// </summary>
    public string? CodigoLinea { get; set; }

    public string Nombre { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    /// <summary>Precio de venta unitario al público.</summary>
    public decimal PrecioBase { get; set; }

    /// <summary>Por debajo de esta cantidad, el stock del local se marca como bajo.</summary>
    public int UmbralStockBajo { get; set; }

    /// <summary>
    /// Baja lógica. Un producto con movimientos históricos no se borra (destruiría la auditoría):
    /// se desactiva. Un producto inactivo no admite movimientos nuevos.
    /// </summary>
    public bool Activo { get; set; } = true;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
