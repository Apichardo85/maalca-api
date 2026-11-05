using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public interface IStoreAccountService
{
    // Cliente (sesión de Supabase; supabaseUserId = claim "sub")
    Task<StoreMeDto?> GetMeAsync(string slug, string supabaseUserId);
    Task<StoreMeDto?> JoinAsync(string slug, string supabaseUserId, string email, JoinStoreRequest request);
    Task<IReadOnlyList<MemberPriceDto>?> GetPricesAsync(string slug, string supabaseUserId);
    Task<IReadOnlyList<StoreOrderSummaryDto>?> GetMyOrdersAsync(string slug, string supabaseUserId);

    // Dueño
    Task<IReadOnlyList<StoreCustomerDto>> ListAsync(Guid affiliateId);
    Task<StoreCustomerDto?> UpdateAsync(Guid affiliateId, Guid id, UpdateStoreCustomerRequest request);
    Task<StoreSettingsDto?> GetSettingsAsync(Guid affiliateId);
    Task<StoreSettingsDto?> UpdateSettingsAsync(Guid affiliateId, StoreSettingsDto request);
}

/// <summary>
/// Cuentas de clientes DENTRO de una tienda (miembros/mayoristas). La identidad es la de Supabase (Google o enlace
/// por correo); la membresía, el estado y la lista de precios viven aquí, por tienda. Ver StoreCustomer.
/// </summary>
public class StoreAccountService : IStoreAccountService
{
    private readonly AppDbContext _db;
    private readonly IOwnerNotificationService _owner;
    private readonly IStoreAccountNotificationService _notify;

    public StoreAccountService(AppDbContext db, IOwnerNotificationService owner, IStoreAccountNotificationService notify)
    {
        _db = db;
        _owner = owner;
        _notify = notify;
    }

    /// <summary>Tienda publicada con el módulo de listas de precio activo; si no, no hay cuentas.</summary>
    private async Task<(Affiliate? Affiliate, bool Enabled)> StoreAsync(string slug)
    {
        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.Slug == slug && a.Published);
        if (affiliate is null) return (null, false);
        var modules = await ModuleAccess.GetActiveModulesAsync(_db, affiliate);
        return (affiliate, modules.Contains(StoreAccounts.ModuleToken, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<StoreMeDto?> GetMeAsync(string slug, string supabaseUserId)
    {
        var (affiliate, enabled) = await StoreAsync(slug);
        if (affiliate is null) return null;
        var mode = NormalizeMode(affiliate.StoreSignupMode);
        if (!enabled) return new StoreMeDto("unavailable", mode, null, null, null, null, null, null);

        var account = await _db.StoreCustomers.AsNoTracking().Include(c => c.PriceList)
            .FirstOrDefaultAsync(c => c.AffiliateId == affiliate.Id && c.SupabaseUserId == supabaseUserId);
        if (account is null) return new StoreMeDto("none", mode, affiliate.StoreSignupNote, null, null, null, null, null);

        // La lista solo se informa si está activa: una lista pausada = precio público (igual que en el pedido).
        var list = account.Status == StoreAccounts.Approved && account.PriceList is { IsActive: true } pl ? pl : null;
        return new StoreMeDto(account.Status, mode, affiliate.StoreSignupNote, account.Name, account.Email,
            list?.Name, list?.DiscountPercent, list?.MinOrderAmount, list?.TaxExempt ?? false);
    }

    public async Task<StoreMeDto?> JoinAsync(string slug, string supabaseUserId, string email, JoinStoreRequest r)
    {
        var (affiliate, enabled) = await StoreAsync(slug);
        if (affiliate is null) return null;
        if (!enabled) throw new ArgumentException("Esta tienda no tiene cuentas de cliente.");

        var name = Clip(r.Name, 120);
        if (name is null || name.Length < 2) throw new ArgumentException("Escribe tu nombre.");
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Tu cuenta no tiene correo.");

        var existing = await _db.StoreCustomers
            .FirstOrDefaultAsync(c => c.AffiliateId == affiliate.Id && c.SupabaseUserId == supabaseUserId);
        if (existing is not null)
        {
            // Rechazado puede volver a pedir (queda pendiente otra vez); aprobado/pendiente no cambia nada.
            if (existing.Status == StoreAccounts.Rejected)
            {
                existing.Status = StoreAccounts.Pending;
                existing.Name = name;
                existing.Phone = Clip(r.Phone, 30);
                existing.BusinessName = Clip(r.BusinessName, 160);
                existing.TaxId = Clip(r.TaxId, 40);
                existing.Message = Clip(r.Message, 500);
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await NotifyOwnerAsync(affiliate, existing);
            }
            return await GetMeAsync(slug, supabaseUserId);
        }

        var open = NormalizeMode(affiliate.StoreSignupMode) == "open";
        var account = new StoreCustomer
        {
            AffiliateId = affiliate.Id,
            SupabaseUserId = supabaseUserId,
            Email = email.Trim().ToLowerInvariant(),
            Name = name,
            Phone = Clip(r.Phone, 30),
            BusinessName = Clip(r.BusinessName, 160),
            TaxId = Clip(r.TaxId, 40),
            Message = Clip(r.Message, 500),
            Status = open ? StoreAccounts.Approved : StoreAccounts.Pending,
            PriceListId = open ? affiliate.StoreDefaultPriceListId : null,
            ApprovedAt = open ? DateTime.UtcNow : null,
        };
        _db.StoreCustomers.Add(account);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Doble clic / dos pestañas: el índice único ya la creó. Se devuelve lo que hay.
            return await GetMeAsync(slug, supabaseUserId);
        }
        if (open)
        {
            await EnsureCustomerAsync(account);
            await _notify.NotifyApprovedAsync(account, affiliate, await PriceListNameAsync(account.PriceListId));
        }
        if (!open) await NotifyOwnerAsync(affiliate, account);
        return await GetMeAsync(slug, supabaseUserId);
    }

    public async Task<IReadOnlyList<MemberPriceDto>?> GetPricesAsync(string slug, string supabaseUserId)
    {
        var (affiliate, enabled) = await StoreAsync(slug);
        if (affiliate is null) return null;
        if (!enabled) return Array.Empty<MemberPriceDto>();
        var resolved = await StoreAccounts.ResolveAsync(_db, affiliate.Id, supabaseUserId);
        if (resolved?.PriceList is not { } list) return Array.Empty<MemberPriceDto>();

        // Mismo universo que el catálogo público: productos publicados y activos.
        var inventory = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.AffiliateId == affiliate.Id && i.IsPubliclyVisible && i.Status == "Active")
            .Select(i => new { i.Id, Price = i.UnitPrice }).ToListAsync();
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.AffiliateId == affiliate.Id && p.IsPubliclyVisible && p.Status == "Active")
            .Select(p => new { p.Id, p.Price }).ToListAsync();
        return inventory.Select(i => new MemberPriceDto(i.Id, PriceListPricing.Resolve(i.Id, i.Price, list)))
            .Concat(products.Select(p => new MemberPriceDto(p.Id, PriceListPricing.Resolve(p.Id, p.Price, list))))
            .ToList();
    }

    public async Task<IReadOnlyList<StoreOrderSummaryDto>?> GetMyOrdersAsync(string slug, string supabaseUserId)
    {
        var (affiliate, _) = await StoreAsync(slug);
        if (affiliate is null) return null;
        var account = await _db.StoreCustomers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id && c.SupabaseUserId == supabaseUserId)
            .Select(c => new { c.Id, c.Email }).FirstOrDefaultAsync();
        if (account is null) return Array.Empty<StoreOrderSummaryDto>();
        var accountId = (Guid?)account.Id;

        // Pedidos hechos antes de tener cuenta (como invitado): se enlazan por el correo ya verificado de la sesión.
        var email = (account.Email ?? string.Empty).Trim().ToLower();
        if (email.Length > 0)
        {
            await _db.Orders
                .Where(o => o.AffiliateId == affiliate.Id && o.StoreCustomerId == null
                            && o.CustomerEmail != null && o.CustomerEmail.ToLower() == email)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.StoreCustomerId, accountId));
        }

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.AffiliateId == affiliate.Id && o.StoreCustomerId == accountId)
            .OrderByDescending(o => o.CreatedAt).Take(50)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, o.Currency, o.ItemsJson, o.FulfillmentType, o.TrackingToken, o.PriceListName })
            .ToListAsync();
        return orders.Select(o =>
        {
            var lines = JsonArrayField.Parse<OrderItemDto>(o.ItemsJson);
            return new StoreOrderSummaryDto(
                o.Id, o.CreatedAt, o.Status.ToString(), o.Total, o.Currency,
                lines.Sum(i => i.Qty),
                o.FulfillmentType, o.TrackingToken, o.PriceListName,
                lines.Select(i => new StoreOrderLineDto(i.ItemId, i.Name, i.Qty, i.Price)).ToList());
        }).ToList();
    }

    public async Task<IReadOnlyList<StoreCustomerDto>> ListAsync(Guid affiliateId)
    {
        var accounts = await _db.StoreCustomers.AsNoTracking().Include(c => c.PriceList)
            .Where(c => c.AffiliateId == affiliateId)
            .OrderBy(c => c.Status == StoreAccounts.Pending ? 0 : c.Status == StoreAccounts.Approved ? 1 : 2)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();
        var ids = accounts.Select(a => (Guid?)a.Id).ToList();
        var counts = await _db.Orders.AsNoTracking()
            .Where(o => o.AffiliateId == affiliateId && ids.Contains(o.StoreCustomerId))
            .GroupBy(o => o.StoreCustomerId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        var byId = counts.Where(c => c.Id != null).ToDictionary(c => c.Id!.Value, c => c.Count);
        return accounts.Select(a => ToDto(a, byId.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<StoreCustomerDto?> UpdateAsync(Guid affiliateId, Guid id, UpdateStoreCustomerRequest r)
    {
        var account = await _db.StoreCustomers.Include(c => c.PriceList)
            .FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        if (account is null) return null;

        var justApproved = false;
        if (r.Status is not null)
        {
            var status = r.Status.Trim().ToLowerInvariant();
            if (status is not (StoreAccounts.Pending or StoreAccounts.Approved or StoreAccounts.Rejected))
                throw new ArgumentException("Estado inválido.");
            justApproved = status == StoreAccounts.Approved && account.Status != StoreAccounts.Approved;
            if (justApproved) account.ApprovedAt = DateTime.UtcNow;
            account.Status = status;
            if (justApproved) await EnsureCustomerAsync(account);
        }
        if (r.ClearPriceList) account.PriceListId = null;
        else if (r.PriceListId is { } listId)
        {
            var owned = await _db.PriceLists.AnyAsync(l => l.Id == listId && l.AffiliateId == affiliateId);
            if (!owned) throw new ArgumentException("Esa lista no es de este negocio.");
            account.PriceListId = listId;
        }
        if (r.Notes is not null) account.Notes = Clip(r.Notes, 1000);
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _db.Entry(account).Reference(c => c.PriceList).LoadAsync();

        // Después de guardar (y con la lista ya cargada), se le avisa al cliente: la tienda le
        // prometió "te avisamos cuando te aprueben".
        if (justApproved)
        {
            var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.Id == affiliateId);
            if (affiliate is not null) await _notify.NotifyApprovedAsync(account, affiliate, account.PriceList?.Name);
        }

        var orders = await _db.Orders.CountAsync(o => o.AffiliateId == affiliateId && o.StoreCustomerId == account.Id);
        return ToDto(account, orders);
    }

    private async Task<string?> PriceListNameAsync(Guid? priceListId) =>
        priceListId is null ? null : await _db.PriceLists.Where(l => l.Id == priceListId).Select(l => l.Name).FirstOrDefaultAsync();

    // Un mayorista aprobado es un cliente del negocio: hasta ahora vivía solo en "Miembros de
    // tienda" y la pantalla de Clientes salía vacía, así que no se le podía facturar ni cobrarle
    // en el punto de venta sin volver a escribirlo a mano. Se da de alta (o se actualiza) al
    // aprobarlo. Falla abierto: si esto revienta, la aprobación ya está guardada.
    private async Task EnsureCustomerAsync(StoreCustomer account)
    {
        try
        {
            var email = account.Email.Trim().ToLowerInvariant();
            var name = string.IsNullOrWhiteSpace(account.BusinessName) ? account.Name : account.BusinessName!;
            var existing = await _db.Customers.FirstOrDefaultAsync(c =>
                c.AffiliateId == account.AffiliateId && c.Email != null && c.Email.ToLower() == email);

            if (existing is null)
            {
                _db.Customers.Add(new Customer
                {
                    AffiliateId = account.AffiliateId,
                    Name = Clip(name, 160) ?? account.Name,
                    Email = email,
                    Phone = account.Phone,
                    Status = "Active",
                });
            }
            else
            {
                // No se pisa lo que el dueño haya escrito a mano: solo se rellenan los huecos.
                if (string.IsNullOrWhiteSpace(existing.Phone)) existing.Phone = account.Phone;
                existing.Status = "Active";
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Carrera entre dos aprobaciones: el cliente ya existe y no hay nada que hacer.
        }
    }

    public async Task<StoreSettingsDto?> GetSettingsAsync(Guid affiliateId)
    {
        var a = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == affiliateId);
        return a is null ? null : new StoreSettingsDto(NormalizeMode(a.StoreSignupMode), a.StoreDefaultPriceListId, a.StoreSignupNote);
    }

    public async Task<StoreSettingsDto?> UpdateSettingsAsync(Guid affiliateId, StoreSettingsDto r)
    {
        var a = await _db.Affiliates.FirstOrDefaultAsync(x => x.Id == affiliateId);
        if (a is null) return null;
        var mode = r.SignupMode?.Trim().ToLowerInvariant();
        if (mode is not ("approval" or "open")) throw new ArgumentException("Modo de registro inválido.");
        if (r.DefaultPriceListId is { } listId && !await _db.PriceLists.AnyAsync(l => l.Id == listId && l.AffiliateId == affiliateId))
            throw new ArgumentException("Esa lista no es de este negocio.");
        a.StoreSignupMode = mode;
        a.StoreDefaultPriceListId = r.DefaultPriceListId;
        a.StoreSignupNote = Clip(r.SignupNote, 500);
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new StoreSettingsDto(mode, a.StoreDefaultPriceListId, a.StoreSignupNote);
    }

    private async Task NotifyOwnerAsync(Affiliate affiliate, StoreCustomer c)
    {
        var who = string.IsNullOrWhiteSpace(c.BusinessName) ? c.Name : $"{c.Name} · {c.BusinessName}";
        await _owner.NotifyAsync(affiliate.Id, "member_request",
            "Nueva solicitud de membresía", who,
            "New membership request", who,
            "price-lists", c.Id);
    }

    private static StoreCustomerDto ToDto(StoreCustomer c, int orders) => new(
        c.Id, c.Name, c.Email, c.Phone, c.BusinessName, c.TaxId, c.Message, c.Status,
        c.PriceListId, c.PriceList?.Name, c.Notes, c.CreatedAt, c.ApprovedAt, orders);

    private static string NormalizeMode(string? mode) => mode == "open" ? "open" : "approval";

    private static string? Clip(string? v, int max)
    {
        var t = v?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }
}
