namespace Maalca.Domain.Enums;

public enum OrderStatus
{
    Pending   = 0, // creado, esperando pago (o pago no requerido — ver Order.RequiresPayment)
    Paid      = 1,
    Fulfilled = 2, // el afiliado marcó el pedido como entregado/completado
    Canceled  = 3,
    // Agregado al final (4), no reordenado — los valores existentes ya están persistidos como
    // int. Flujo real para Kitchen Display: Pending -> Paid -> Preparing -> Fulfilled -> Canceled,
    // pero el orden numérico del enum no importa, solo que no se reasignen los primeros 4.
    Preparing = 4, // el afiliado marcó el pedido como "en preparación" (Kitchen Display)
    // Listo para recoger/servir, aún sin entregar. Flujo: Pending -> Paid -> Preparing -> Ready -> Fulfilled.
    // Persistido como int (5): sin migración de esquema.
    Ready     = 5,
}
