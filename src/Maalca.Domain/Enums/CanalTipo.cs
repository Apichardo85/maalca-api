namespace Maalca.Domain.Enums;

public enum CanalTipo
{
    WhatsApp  = 0,
    Email     = 1,
    Telefono  = 2,
    Facebook  = 3,
    Instagram = 4,
    TikTok    = 5,
    // Servicios externos de pedido/delivery (enlace a la tienda del negocio en cada uno).
    // Agregados al final, sin reordenar: Tipo se persiste como int en la tabla Canales.
    DoorDash  = 6,
    UberEats  = 7,
    Grubhub   = 8
}
