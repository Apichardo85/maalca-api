using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Common;

/// <summary>Lista de precios ya cargada para recalcular un pedido (sin ir a la base por cada línea).</summary>
public sealed record PriceListContext(
    Guid Id, string Name, decimal DiscountPercent, decimal? MinOrderAmount, IReadOnlyDictionary<Guid, decimal> FixedPrices);

/// <summary>
/// El ÚNICO cálculo de precio por lista (miembro/mayorista): lo usan el pedido (OrderService) y la vista previa del
/// dashboard, así lo que el dueño ve es lo que se cobra. Precio fijo del producto en la lista > descuento general > público.
/// </summary>
public static class PriceListPricing
{
    public static decimal Resolve(Guid itemId, decimal publicPrice, PriceListContext? list)
    {
        if (list is null) return publicPrice;
        if (list.FixedPrices.TryGetValue(itemId, out var fixedPrice)) return Math.Round(fixedPrice, 2);
        var pct = Math.Clamp(list.DiscountPercent, 0m, 100m);
        return Math.Round(publicPrice * (1m - pct / 100m), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Carga una lista ACTIVA del negocio. Null si no existe, es de otro negocio o está apagada.</summary>
    public static async Task<PriceListContext?> LoadAsync(AppDbContext db, Guid affiliateId, Guid priceListId)
    {
        var list = await db.PriceLists.AsNoTracking()
            .Where(l => l.Id == priceListId && l.AffiliateId == affiliateId && l.IsActive)
            .Select(l => new { l.Id, l.Name, l.DiscountPercent, l.MinOrderAmount })
            .FirstOrDefaultAsync();
        if (list is null) return null;
        var fixedPrices = await db.PriceListItems.AsNoTracking()
            .Where(i => i.PriceListId == list.Id)
            .ToDictionaryAsync(i => i.ItemId, i => i.Price);
        return new PriceListContext(list.Id, list.Name, list.DiscountPercent, list.MinOrderAmount, fixedPrices);
    }
}

/// <summary>Costo de un método de envío para un subtotal (gratis desde FreeOverAmount, si está fijado).</summary>
public static class ShippingPricing
{
    public static decimal Cost(decimal price, decimal? freeOverAmount, decimal subtotal) =>
        freeOverAmount is decimal free && free > 0 && subtotal >= free ? 0m : Math.Round(Math.Max(0m, price), 2);

    /// <summary>Link público de rastreo de la paquetería (null si es "other" o no hay número).</summary>
    public static string? TrackingUrl(string? carrier, string? trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) return null;
        var n = Uri.EscapeDataString(trackingNumber.Trim());
        return carrier?.ToLowerInvariant() switch
        {
            "usps" => $"https://tools.usps.com/go/TrackConfirmAction?tLabels={n}",
            "ups" => $"https://www.ups.com/track?tracknum={n}",
            "fedex" => $"https://www.fedex.com/fedextrack/?trknbr={n}",
            "dhl" => $"https://www.dhl.com/us-en/home/tracking/tracking-express.html?submit=1&tracking-id={n}",
            _ => null,
        };
    }

    public static readonly string[] Carriers = { "usps", "ups", "fedex", "dhl", "other" };
}
