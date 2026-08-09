using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Fase 9 Etapa A (Pantallas — Menu Board con comerciales). Un comercial/promo que se intercala
/// entre los slides normales de categoría en el Menu Board público — no es un producto del
/// catálogo (no tiene precio, no vive en Products/Services/InventoryItem). StartsAt/EndsAt son
/// opcionales: null = siempre vigente; con fechas, sirve para una promo con vencimiento sin
/// tener que acordarse de desactivarla a mano.
/// </summary>
public class ScreenAd : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    public string MediaUrl { get; set; } = string.Empty;
    public ScreenAdMediaType MediaType { get; set; } = ScreenAdMediaType.Image;
    public int DurationSeconds { get; set; } = 8;
    public int SortOrder { get; set; } = 0;
    public bool Active { get; set; } = true;

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}
