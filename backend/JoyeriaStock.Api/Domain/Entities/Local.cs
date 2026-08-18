namespace JoyeriaStock.Api.Domain.Entities;

/// <summary>Un local físico de la joyería.</summary>
public class Local
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Direccion { get; set; } = null!;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
