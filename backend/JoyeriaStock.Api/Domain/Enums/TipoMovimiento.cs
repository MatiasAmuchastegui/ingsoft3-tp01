namespace JoyeriaStock.Api.Domain.Enums;

public enum TipoMovimiento
{
    /// <summary>Ingreso de mercadería: suma stock.</summary>
    Entrada = 1,

    /// <summary>Egreso que no es venta (rotura, ajuste, devolución a proveedor): resta stock.</summary>
    Salida = 2,

    /// <summary>Venta a un cliente: resta stock y además registra precio unitario y total.</summary>
    Venta = 3,

    /// <summary>
    /// Egreso por traslado a otro local: resta stock. Nunca aparece solo — siempre viene
    /// en par con una <see cref="TransferenciaEntrada"/> que comparte el mismo
    /// <c>TransferenciaId</c>, creada en la misma transacción.
    /// </summary>
    TransferenciaSalida = 4,

    /// <summary>Ingreso por traslado desde otro local: suma stock. Es el par del anterior.</summary>
    TransferenciaEntrada = 5
}
