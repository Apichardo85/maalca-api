namespace Maalca.Domain.Enums;

public enum OrderStatus
{
    Pending   = 0, // creado, esperando pago (o pago no requerido — ver Order.RequiresPayment)
    Paid      = 1,
    Fulfilled = 2, // el afiliado marcó el pedido como entregado/completado
    Canceled  = 3
}
