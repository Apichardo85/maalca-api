using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Common;

/// <summary>Lista de precios ya cargada para recalcular un pedido (sin ir a la base por cada línea).</summary>
public sealed record PriceListContext(
    Guid Id, string Name, decimal DiscountPercent, decimal? MinOrderAmount, IReadOnlyDictionary<Guid, decimal> FixedPrices,
    bool TaxExempt = false);

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
            .Select(l => new { l.Id, l.Name, l.DiscountPercent, l.MinOrderAmount, l.TaxExempt })
            .FirstOrDefaultAsync();
        if (list is null) return null;
        var fixedPrices = await db.PriceListItems.AsNoTracking()
            .Where(i => i.PriceListId == list.Id)
            .ToDictionaryAsync(i => i.ItemId, i => i.Price);
        return new PriceListContext(list.Id, list.Name, list.DiscountPercent, list.MinOrderAmount, fixedPrices, list.TaxExempt);
    }
}

/// <summary>
/// Impuesto de venta del pedido online, calculado por el API (nunca el navegador) cuando el negocio fijó su tasa.
/// Base = subtotal (+ envío si el negocio grava el envío). Lista exenta (reventa) = 0.
/// </summary>
public static class SalesTax
{
    public static decimal Compute(decimal ratePercent, decimal subtotal, decimal shipping, bool taxShipping, bool exempt)
    {
        if (exempt || ratePercent <= 0) return 0m;
        var taxBase = subtotal + (taxShipping ? shipping : 0m);
        return Math.Round(taxBase * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Costo de un método de envío para un subtotal (gratis desde FreeOverAmount, si está fijado).</summary>
public static class ShippingPricing
{
    public static decimal Cost(decimal price, decimal? freeOverAmount, decimal subtotal) =>
        freeOverAmount is decimal free && free > 0 && subtotal >= free ? 0m : Math.Round(Math.Max(0m, price), 2);

    /// <summary>Costo con tarifa por peso: el primer escalón cuyo tope (lb) cubre el peso; pasado el último, el último.
    /// Sin escalones o sin peso conocido, el precio fijo del método.</summary>
    public static decimal Cost(decimal price, decimal? freeOverAmount, decimal subtotal, string? weightRates, decimal weightOz)
    {
        if (freeOverAmount is decimal free && free > 0 && subtotal >= free) return 0m;
        var tiers = ParseWeightRates(weightRates);
        if (tiers.Count == 0 || weightOz <= 0) return Math.Round(Math.Max(0m, price), 2);
        var lb = weightOz / 16m;
        foreach (var t in tiers) if (lb <= t.UpToLb) return t.Price;
        return tiers[^1].Price;
    }

    /// <summary>"1:6.50; 3:9.99; 10:18" → [(1, 6.50), (3, 9.99), (10, 18)] ordenado por peso. Ignora entradas inválidas.</summary>
    public static List<(decimal UpToLb, decimal Price)> ParseWeightRates(string? raw)
    {
        var list = new List<(decimal, decimal)>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        foreach (var part in raw.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;
            if (decimal.TryParse(kv[0], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var lb) && lb > 0
                && decimal.TryParse(kv[1].TrimStart('$'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var pr) && pr >= 0)
                list.Add((lb, Math.Round(pr, 2)));
        }
        return list.OrderBy(t => t.Item1).ToList();
    }

    /// <summary>Normaliza "nj, ny ,PA" → "NJ,NY,PA" (solo códigos de 2 letras, sin repetidos). Null si queda vacío.</summary>
    public static string? NormalizeStates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var states = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant()).Where(s => s.Length == 2 && s.All(char.IsLetter)).Distinct().ToList();
        return states.Count == 0 ? null : string.Join(",", states);
    }

    /// <summary>¿Aplica el método al estado de destino? Sin estados configurados aplica a todos.</summary>
    public static bool ServesState(string? methodStates, string? destState) =>
        string.IsNullOrWhiteSpace(methodStates)
        || (!string.IsNullOrWhiteSpace(destState)
            && methodStates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(destState.Trim(), StringComparer.OrdinalIgnoreCase));

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
