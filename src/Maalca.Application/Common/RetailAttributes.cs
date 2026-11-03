using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common;

/// <summary>
/// Lectura/escritura de los atributos de tienda de un InventoryItem en un solo lugar (dashboard, catálogo
/// público y pedidos leen lo mismo). Normaliza y valida: textos recortados, medidas >= 0, reglas de cantidad
/// coherentes (mínimo <= máximo, múltiplo >= 2) y tokens conocidos para hazmat/asfixia.
/// </summary>
public static class RetailAttributes
{
    public const string HazmatLimitedQuantity = "limited_quantity";
    public static readonly string[] HazmatValues = { HazmatLimitedQuantity };
    public static readonly string[] ChokingValues = { "small_parts", "small_ball", "marble", "balloon" };

    public static RetailAttributesDto? From(InventoryItem i)
    {
        var dto = new RetailAttributesDto(
            i.Brand, i.WeightOz, i.LengthIn, i.WidthIn, i.HeightIn, i.Hazmat, i.ChokingWarning,
            i.VariantGroup, i.VariantLabel, i.MinOrderQty, i.OrderQtyMultiple, i.MaxPerOrder);
        var tiers = ParseTiers(i.VolumeTiers);
        // Sin ningún atributo: null, para no mandar un objeto vacío por cada producto del catálogo.
        if (tiers.Count == 0) return dto == new RetailAttributesDto() ? null : dto;
        return dto with { VolumeTiers = tiers };
    }

    public static void Apply(InventoryItem item, RetailAttributesDto r)
    {
        item.Brand = Text(r.Brand, 120);
        item.WeightOz = Measure(r.WeightOz, "El peso");
        item.LengthIn = Measure(r.LengthIn, "El largo");
        item.WidthIn = Measure(r.WidthIn, "El ancho");
        item.HeightIn = Measure(r.HeightIn, "El alto");
        item.Hazmat = Token(r.Hazmat, HazmatValues, "Tipo de material peligroso inválido.");
        item.ChokingWarning = Token(r.ChokingWarning, ChokingValues, "Advertencia de asfixia inválida.");
        item.VariantGroup = Text(r.VariantGroup, 80);
        item.VariantLabel = Text(r.VariantLabel, 60);

        var min = Positive(r.MinOrderQty);
        var max = Positive(r.MaxPerOrder);
        var multiple = Positive(r.OrderQtyMultiple);
        if (multiple == 1) multiple = null; // múltiplo de 1 = sin regla
        if (min is not null && max is not null && min > max)
            throw new ArgumentException("El mínimo por pedido no puede ser mayor que el máximo.");
        if (multiple is not null && max is not null && max < multiple)
            throw new ArgumentException("El máximo por pedido no alcanza ni para un múltiplo.");
        item.MinOrderQty = min;
        item.MaxPerOrder = max;
        item.OrderQtyMultiple = multiple;
        item.VolumeTiers = SerializeTiers(r.VolumeTiers);
    }

    public const int MaxVolumeTiers = 5;

    /// <summary>Escalones guardados, limpios y ordenados por cantidad (JSON roto o vacío = lista vacía).</summary>
    public static List<VolumeTierDto> ParseTiers(string? json) =>
        JsonArrayField.Parse<VolumeTierDto>(json)
            .Where(t => t.MinQty >= 2 && t.Price > 0)
            .GroupBy(t => t.MinQty).Select(g => g.First())
            .OrderBy(t => t.MinQty)
            .Take(MaxVolumeTiers)
            .ToList();

    /// <summary>Valida y serializa los escalones (null/[] = sin escalones). Lanza ArgumentException con el motivo.</summary>
    public static string? SerializeTiers(IReadOnlyList<VolumeTierDto>? tiers)
    {
        if (tiers is null || tiers.Count == 0) return null;
        if (tiers.Count > MaxVolumeTiers)
            throw new ArgumentException($"Máximo {MaxVolumeTiers} escalones de precio por cantidad.");
        var clean = new List<VolumeTierDto>();
        foreach (var t in tiers.OrderBy(t => t.MinQty))
        {
            if (t.MinQty < 2 || t.MinQty > 100000)
                throw new ArgumentException("Cada escalón de precio por cantidad empieza en 2 unidades o más.");
            if (t.Price <= 0 || t.Price > 1_000_000)
                throw new ArgumentException("Cada escalón de precio por cantidad necesita un precio mayor que 0.");
            if (clean.Any(c => c.MinQty == t.MinQty))
                throw new ArgumentException($"Hay dos escalones que empiezan en {t.MinQty} unidades.");
            if (clean.Count > 0 && t.Price >= clean[^1].Price)
                throw new ArgumentException("Cada escalón debe tener un precio menor que el anterior.");
            clean.Add(new VolumeTierDto(t.MinQty, Math.Round(t.Price, 2)));
        }
        return JsonArrayField.Serialize(clean);
    }

    /// <summary>
    /// Precio por unidad que corresponde a <paramref name="qty"/> unidades del producto según sus escalones (el
    /// escalón más alto alcanzado). Null = la cantidad no llega a ningún escalón (rige el precio normal).
    /// Lo usan el pedido (OrderService) y, en espejo, el navegador (lib/commerce.ts tierPriceFor).
    /// </summary>
    public static decimal? TierPrice(IReadOnlyList<VolumeTierDto> tiers, int qty)
    {
        decimal? price = null;
        foreach (var t in tiers.OrderBy(t => t.MinQty))
            if (qty >= t.MinQty) price = t.Price;
        return price;
    }

    /// <summary>
    /// Reglas de cantidad de un producto para la cantidad TOTAL pedida de ese producto (sumando líneas).
    /// Lanza ArgumentException con un mensaje que el checkout muestra tal cual.
    /// </summary>
    public static void ValidateQuantity(string name, int qty, int? min, int? multiple, int? max)
    {
        if (min is int mn && qty < mn)
            throw new ArgumentException($"\"{name}\": el pedido mínimo es de {mn} unidades.");
        if (multiple is int mu && mu > 1 && qty % mu != 0)
            throw new ArgumentException($"\"{name}\": se vende en múltiplos de {mu}.");
        if (max is int mx && qty > mx)
            throw new ArgumentException($"\"{name}\": máximo {mx} por pedido.");
    }

    private static string? Text(string? v, int maxLength)
    {
        var t = v?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > maxLength ? t[..maxLength] : t;
    }

    private static decimal? Measure(decimal? v, string label)
    {
        if (v is null || v == 0) return null;
        if (v < 0 || v > 100000) throw new ArgumentException($"{label} no es válido.");
        return Math.Round(v.Value, 2);
    }

    private static int? Positive(int? v) => v is int n && n > 0 ? n : null;

    private static string? Token(string? v, string[] allowed, string error)
    {
        var t = v?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t) || t == "none") return null;
        if (!allowed.Contains(t)) throw new ArgumentException(error);
        return t;
    }
}
