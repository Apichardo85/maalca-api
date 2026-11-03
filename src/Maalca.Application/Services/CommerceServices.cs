using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>Listas de precios (Miembro, Mayorista…) del negocio — dashboard → Listas de precio.</summary>
public class PriceListService : IPriceListService
{
    private readonly AppDbContext _db;
    public PriceListService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PriceListDto>> GetAllAsync(Guid affiliateId)
    {
        var lists = await _db.PriceLists.AsNoTracking().Include(l => l.Items)
            .Where(l => l.AffiliateId == affiliateId)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt)
            .ToListAsync();
        return lists.Select(ToDto).ToList();
    }

    public async Task<PriceListDto> CreateAsync(Guid affiliateId, UpsertPriceListRequest request)
    {
        var list = new PriceList { AffiliateId = affiliateId };
        await ApplyAsync(affiliateId, list, request);
        _db.PriceLists.Add(list);
        await _db.SaveChangesAsync();
        return ToDto(list);
    }

    public async Task<PriceListDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertPriceListRequest request)
    {
        var list = await _db.PriceLists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id && l.AffiliateId == affiliateId);
        if (list is null) return null;
        await ApplyAsync(affiliateId, list, request);
        list.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(list);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var list = await _db.PriceLists.FirstOrDefaultAsync(l => l.Id == id && l.AffiliateId == affiliateId);
        if (list is null) return false;
        _db.PriceLists.Remove(list); // PriceListItems caen en cascada; los pedidos guardan su copia del nombre.
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<PriceListPreviewLineDto>?> PreviewAsync(Guid affiliateId, Guid id)
    {
        var exists = await _db.PriceLists.AnyAsync(l => l.Id == id && l.AffiliateId == affiliateId);
        if (!exists) return null;
        // La vista previa usa la lista aunque esté apagada: el dueño la revisa antes de prenderla.
        var raw = await _db.PriceLists.AsNoTracking().Include(l => l.Items).FirstAsync(l => l.Id == id);
        var context = new PriceListContext(raw.Id, raw.Name, raw.DiscountPercent, raw.MinOrderAmount,
            raw.Items.ToDictionary(i => i.ItemId, i => i.Price), raw.TaxExempt);

        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && i.Status == "Active")
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.VariantLabel, i.UnitPrice })
            .ToListAsync();
        return items.Select(i => new PriceListPreviewLineDto(
            i.Id, i.Name, i.VariantLabel, i.UnitPrice,
            context.FixedPrices.TryGetValue(i.Id, out var f) ? f : null,
            PriceListPricing.Resolve(i.Id, i.UnitPrice, context))).ToList();
    }

    private async Task ApplyAsync(Guid affiliateId, PriceList list, UpsertPriceListRequest r)
    {
        var name = r.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("La lista necesita un nombre.");
        if (name.Length > 120) throw new ArgumentException("El nombre es demasiado largo.");
        if (r.DiscountPercent < 0 || r.DiscountPercent > 100) throw new ArgumentException("El descuento debe estar entre 0 y 100%.");
        if (r.MinOrderAmount is < 0) throw new ArgumentException("El pedido mínimo no puede ser negativo.");

        list.Name = name;
        list.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim()[..Math.Min(500, r.Description.Trim().Length)];
        list.DiscountPercent = Math.Round(r.DiscountPercent, 2);
        list.MinOrderAmount = r.MinOrderAmount is decimal m && m > 0 ? Math.Round(m, 2) : null;
        list.IsActive = r.IsActive;
        list.SortOrder = r.SortOrder;
        list.TaxExempt = r.TaxExempt;

        if (r.Items is null) return; // null = no tocar los precios fijos
        if (r.Items.Any(i => i.Price < 0)) throw new ArgumentException("Un precio fijo no puede ser negativo.");
        var wanted = r.Items.GroupBy(i => i.ItemId).Select(g => g.Last()).ToList();
        var ids = wanted.Select(i => i.ItemId).ToList();
        var owned = await _db.InventoryItems.Where(i => i.AffiliateId == affiliateId && ids.Contains(i.Id)).Select(i => i.Id).ToListAsync();
        owned.AddRange(await _db.Products.Where(p => p.AffiliateId == affiliateId && ids.Contains(p.Id)).Select(p => p.Id).ToListAsync());
        if (ids.Except(owned).Any()) throw new ArgumentException("Uno de los productos no pertenece a este negocio.");

        // Reemplazo completo: lo que no viene se quita, lo que viene se crea o actualiza.
        foreach (var existing in list.Items.Where(e => !ids.Contains(e.ItemId)).ToList()) _db.PriceListItems.Remove(existing);
        foreach (var w in wanted)
        {
            var existing = list.Items.FirstOrDefault(e => e.ItemId == w.ItemId);
            if (existing is not null) { existing.Price = Math.Round(w.Price, 2); existing.UpdatedAt = DateTime.UtcNow; }
            else list.Items.Add(new PriceListItem { PriceListId = list.Id, ItemId = w.ItemId, Price = Math.Round(w.Price, 2) });
        }
    }

    private static PriceListDto ToDto(PriceList l) => new(
        l.Id, l.Name, l.Description, l.DiscountPercent, l.MinOrderAmount, l.IsActive, l.SortOrder,
        l.Items.Select(i => new PriceListItemDto(i.ItemId, i.Price)).ToList(), l.TaxExempt);
}

/// <summary>Métodos de envío del negocio (costo fijo / gratis desde $X) — dashboard → Envíos.</summary>
public class ShippingService : IShippingService
{
    private readonly AppDbContext _db;
    public ShippingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShippingMethodDto>> GetMethodsAsync(Guid affiliateId) =>
        (await _db.ShippingMethods.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .ToListAsync()).Select(ToDto).ToList();

    public async Task<ShippingMethodDto> CreateAsync(Guid affiliateId, UpsertShippingMethodRequest request)
    {
        var method = new ShippingMethod { AffiliateId = affiliateId };
        Apply(method, request);
        _db.ShippingMethods.Add(method);
        await _db.SaveChangesAsync();
        return ToDto(method);
    }

    public async Task<ShippingMethodDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertShippingMethodRequest request)
    {
        var method = await _db.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id && m.AffiliateId == affiliateId);
        if (method is null) return null;
        Apply(method, request);
        method.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(method);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var method = await _db.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id && m.AffiliateId == affiliateId);
        if (method is null) return false;
        _db.ShippingMethods.Remove(method); // los pedidos guardan su copia del nombre y del costo
        await _db.SaveChangesAsync();
        return true;
    }

    private static void Apply(ShippingMethod m, UpsertShippingMethodRequest r)
    {
        var name = r.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("El método de envío necesita un nombre.");
        if (name.Length > 120) throw new ArgumentException("El nombre es demasiado largo.");
        if (r.Price < 0) throw new ArgumentException("El costo de envío no puede ser negativo.");
        if (r.FreeOverAmount is < 0) throw new ArgumentException("El monto para envío gratis no puede ser negativo.");
        if (r.MinDays is < 0 || r.MaxDays is < 0 || (r.MinDays is int a && r.MaxDays is int b && a > b))
            throw new ArgumentException("Revisa los días de entrega (mínimo no puede ser mayor que máximo).");

        m.Name = name;
        m.NameEn = string.IsNullOrWhiteSpace(r.NameEn) ? null : r.NameEn.Trim();
        m.Price = Math.Round(r.Price, 2);
        m.FreeOverAmount = r.FreeOverAmount is decimal f && f > 0 ? Math.Round(f, 2) : null;
        m.MinDays = r.MinDays;
        m.MaxDays = r.MaxDays;
        m.AllowsHazmat = r.AllowsHazmat;
        m.IsActive = r.IsActive;
        m.SortOrder = r.SortOrder;
    }

    internal static ShippingMethodDto ToDto(ShippingMethod m) => new(
        m.Id, m.Name, m.NameEn, m.Price, m.FreeOverAmount, m.MinDays, m.MaxDays, m.AllowsHazmat, m.IsActive, m.SortOrder);
}
