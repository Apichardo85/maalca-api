using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
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
    Task<StoreMeDto?> UpdateMyProfileAsync(string slug, string supabaseUserId, UpdateMyStoreProfileRequest request);

    // Dueño
    Task<IReadOnlyList<StoreCustomerDto>> ListAsync(Guid affiliateId);
    Task<StoreCustomerDto?> UpdateAsync(Guid affiliateId, Guid id, UpdateStoreCustomerRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);
    Task<StoreSettingsDto?> GetSettingsAsync(Guid affiliateId);
    Task<StoreSettingsDto?> UpdateSettingsAsync(Guid affiliateId, StoreSettingsDto request);

    // Ficha del cliente, POS y reportes
    Task<CustomerInsightsDto?> GetCustomerInsightsAsync(Guid affiliateId, Guid customerId);
    Task<CustomerPriceListDto?> GetCustomerPriceListAsync(Guid affiliateId, Guid customerId);
    Task<bool> ClearCustomerNoShowsAsync(Guid affiliateId, Guid customerId);
    Task<IReadOnlyList<CustomerSalesRowDto>> GetSalesReportAsync(Guid affiliateId, DateTime? from, DateTime? to);
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
            list?.Name, list?.DiscountPercent, list?.MinOrderAmount, list?.TaxExempt ?? false,
            account.Phone, account.BusinessName, account.TaxId);
    }

    public async Task<StoreMeDto?> UpdateMyProfileAsync(string slug, string supabaseUserId, UpdateMyStoreProfileRequest r)
    {
        var (affiliate, _) = await StoreAsync(slug);
        if (affiliate is null) return null;
        var account = await _db.StoreCustomers
            .FirstOrDefaultAsync(c => c.AffiliateId == affiliate.Id && c.SupabaseUserId == supabaseUserId);
        if (account is null) return null;
        if (r.Name is not null)
        {
            var name = Clip(r.Name, 120);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Escribe tu nombre.");
            account.Name = name;
        }
        if (r.Phone is not null) account.Phone = Clip(r.Phone, 30);
        if (r.BusinessName is not null) account.BusinessName = Clip(r.BusinessName, 160);
        if (r.TaxId is not null) account.TaxId = Clip(r.TaxId, 40);
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetMeAsync(slug, supabaseUserId);
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
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, o.Currency, o.ItemsJson, o.FulfillmentType, o.TrackingToken, o.PriceListName, o.ShippingCarrier, o.TrackingNumber, o.PaymentMethod, o.CollectedAt })
            .ToListAsync();
        var parsed = orders.Select(o => (Order: o, Lines: JsonArrayField.Parse<OrderItemDto>(o.ItemsJson))).ToList();

        // Miniaturas: la foto actual de cada producto (inventario o producto), una sola consulta por tabla.
        var ids = parsed.SelectMany(p => p.Lines).Select(l => Guid.TryParse(l.ItemId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ids.Count > 0)
        {
            var inv = await _db.InventoryItems.AsNoTracking()
                .Where(i => i.AffiliateId == affiliate.Id && ids.Contains(i.Id) && i.ImageUrl != null)
                .Select(i => new { i.Id, i.ImageUrl }).ToListAsync();
            var prod = await _db.Products.AsNoTracking()
                .Where(p => p.AffiliateId == affiliate.Id && ids.Contains(p.Id) && p.ImageUrl != null)
                .Select(p => new { p.Id, p.ImageUrl }).ToListAsync();
            foreach (var i in inv) images[i.Id.ToString()] = i.ImageUrl!;
            foreach (var p in prod) images.TryAdd(p.Id.ToString(), p.ImageUrl!);
        }

        return parsed.Select(p =>
        {
            var o = p.Order;
            return new StoreOrderSummaryDto(
                o.Id, o.CreatedAt, o.Status.ToString(), o.Total, o.Currency,
                p.Lines.Sum(i => i.Qty),
                o.FulfillmentType, o.TrackingToken, o.PriceListName,
                p.Lines.Select(i => new StoreOrderLineDto(i.ItemId, i.Name, i.Qty, i.Price,
                    images.TryGetValue(i.ItemId ?? string.Empty, out var img) ? img : null)).ToList(),
                o.ShippingCarrier, o.TrackingNumber,
                IsPurchase(o.Status, o.PaymentMethod, o.CollectedAt));
        }).ToList();
    }

    /// <summary>Quita al miembro de la tienda. Sus pedidos se conservan (solo pierden el enlace a la cuenta); si vuelve, pide acceso de nuevo.</summary>
    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var account = await _db.StoreCustomers.FirstOrDefaultAsync(c => c.AffiliateId == affiliateId && c.Id == id);
        if (account is null) return false;
        await _db.Orders
            .Where(o => o.AffiliateId == affiliateId && o.StoreCustomerId == id)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.StoreCustomerId, (Guid?)null));
        _db.StoreCustomers.Remove(account);
        await _db.SaveChangesAsync();
        return true;
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

    // ── Ficha del cliente / POS / reportes ─────────────────────────────────────────────────────

    /// <summary>Un pedido cuenta como compra si no está cancelado ni pendiente y, si se paga en el local, ya se cobró
    /// (mismo criterio que "Gastado" en la ficha).</summary>
    private static bool IsPurchase(OrderStatus status, string? paymentMethod, DateTime? collectedAt) =>
        (status is OrderStatus.Paid or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Fulfilled)
        && !((paymentMethod == "PayAtPickup" || paymentMethod == "PayAtTable") && collectedAt is null);

    private async Task<StoreCustomer?> AccountForCustomerAsync(Guid affiliateId, string? email)
    {
        var e = email?.Trim().ToLower();
        if (string.IsNullOrEmpty(e)) return null;
        return await _db.StoreCustomers.AsNoTracking().Include(c => c.PriceList)
            .FirstOrDefaultAsync(c => c.AffiliateId == affiliateId && c.Email.ToLower() == e);
    }

    public async Task<CustomerInsightsDto?> GetCustomerInsightsAsync(Guid affiliateId, Guid customerId)
    {
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId && c.AffiliateId == affiliateId)
            .Select(c => new { c.Email, c.NoShows }).FirstOrDefaultAsync();
        if (customer is null) return null;

        var account = await AccountForCustomerAsync(affiliateId, customer.Email);
        var accountDto = account is null ? null : new CustomerStoreAccountDto(
            account.Id, account.Status, account.BusinessName, account.TaxId, account.PriceListId, account.PriceList?.Name);

        var raw = await _db.Orders.AsNoTracking()
            .Where(o => o.AffiliateId == affiliateId && o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, o.PaymentMethod, o.CollectedAt, o.ItemsJson })
            .ToListAsync();

        var orders = raw.Select(o => new
        {
            o.Id, o.CreatedAt, o.Status, o.Total,
            Counted = IsPurchase(o.Status, o.PaymentMethod, o.CollectedAt),
            Lines = JsonArrayField.Parse<OrderItemDto>(o.ItemsJson),
        }).ToList();
        var bought = orders.Where(o => o.Counted).OrderBy(o => o.CreatedAt).ToList();
        var spent = bought.Sum(o => o.Total);
        double? avgDays = bought.Count > 1
            ? (bought[^1].CreatedAt - bought[0].CreatedAt).TotalDays / (bought.Count - 1)
            : null;
        var stats = new CustomerStatsDto(
            orders.Count(o => o.Status != OrderStatus.Canceled), bought.Count, spent,
            bought.Count > 0 ? Math.Round(spent / bought.Count, 2) : 0,
            bought.FirstOrDefault()?.CreatedAt, bought.LastOrDefault()?.CreatedAt,
            avgDays is null or < 1 ? null : Math.Round(avgDays.Value, 1));

        // Últimos 12 meses (incluye meses en cero para que la gráfica no salte).
        var now = DateTime.UtcNow;
        var monthly = Enumerable.Range(0, 12).Select(i => new DateTime(now.Year, now.Month, 1).AddMonths(i - 11))
            .Select(m => new CustomerMonthDto(
                m.ToString("yyyy-MM"),
                bought.Where(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month).Sum(o => o.Total),
                bought.Count(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month)))
            .ToList();

        var top = bought.SelectMany(o => o.Lines.Select(l => new { l.Name, l.Qty, Amount = l.Price * l.Qty, o.CreatedAt }))
            .GroupBy(l => l.Name)
            .Select(g => new CustomerTopProductDto(g.Key, g.Sum(x => x.Qty), g.Sum(x => x.Amount), g.Max(x => x.CreatedAt)))
            .OrderByDescending(p => p.Qty).ThenByDescending(p => p.Amount)
            .Take(10).ToList();

        var detail = orders.Take(100).Select(o => new CustomerOrderDetailDto(
            o.Id, o.CreatedAt, o.Status.ToString(), o.Total, o.Counted,
            o.Lines.Select(l => new CustomerOrderLineDto(l.Name, l.Qty, l.Price)).ToList())).ToList();

        return new CustomerInsightsDto(accountDto, stats, monthly, top, detail, customer.NoShows);
    }

    /// <summary>Quita las marcas "No vino" del cliente para que pueda volver a pedir pagando al recoger.</summary>
    public async Task<bool> ClearCustomerNoShowsAsync(Guid affiliateId, Guid customerId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.AffiliateId == affiliateId);
        if (customer is null) return false;
        customer.NoShows = 0;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CustomerPriceListDto?> GetCustomerPriceListAsync(Guid affiliateId, Guid customerId)
    {
        var email = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId && c.AffiliateId == affiliateId)
            .Select(c => c.Email).FirstOrDefaultAsync();
        var account = await AccountForCustomerAsync(affiliateId, email);
        if (account is null || account.Status != StoreAccounts.Approved || account.PriceListId is not { } listId) return null;
        var list = await PriceListPricing.LoadAsync(_db, affiliateId, listId);
        if (list is null) return null;

        // Todo el catálogo (el POS vende también lo no publicado).
        var inventory = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && i.Status == "Active")
            .Select(i => new { i.Id, Price = i.UnitPrice }).ToListAsync();
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.Status == "Active")
            .Select(p => new { p.Id, p.Price }).ToListAsync();
        var prices = inventory.Select(i => new MemberPriceDto(i.Id, PriceListPricing.Resolve(i.Id, i.Price, list)))
            .Concat(products.Select(p => new MemberPriceDto(p.Id, PriceListPricing.Resolve(p.Id, p.Price, list))))
            .ToList();
        return new CustomerPriceListDto(list.Name, list.MinOrderAmount, list.TaxExempt, prices);
    }

    public async Task<IReadOnlyList<CustomerSalesRowDto>> GetSalesReportAsync(Guid affiliateId, DateTime? from, DateTime? to)
    {
        var q = _db.Orders.AsNoTracking().Where(o => o.AffiliateId == affiliateId && o.CustomerId != null);
        if (from is { } f) q = q.Where(o => o.CreatedAt >= f);
        if (to is { } t) q = q.Where(o => o.CreatedAt < t.Date.AddDays(1));
        var raw = await q.Select(o => new { CustomerId = o.CustomerId!.Value, o.Status, o.Total, o.PaymentMethod, o.CollectedAt, o.CreatedAt })
            .ToListAsync();
        var bought = raw.Where(o => IsPurchase(o.Status, o.PaymentMethod, o.CollectedAt)).ToList();
        var ids = bought.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Email, c.Phone }).ToListAsync();
        var emails = customers.Where(c => c.Email != null).Select(c => c.Email!.ToLower()).ToList();
        var business = await _db.StoreCustomers.AsNoTracking()
            .Where(s => s.AffiliateId == affiliateId && emails.Contains(s.Email.ToLower()))
            .Select(s => new { Email = s.Email.ToLower(), s.BusinessName }).ToListAsync();

        return bought.GroupBy(o => o.CustomerId).Select(g =>
        {
            var c = customers.FirstOrDefault(x => x.Id == g.Key);
            var biz = c?.Email is { } e ? business.FirstOrDefault(b => b.Email == e.ToLower())?.BusinessName : null;
            var spent = g.Sum(o => o.Total);
            return new CustomerSalesRowDto(g.Key, c?.Name ?? "—", c?.Email, c?.Phone, biz,
                g.Count(), spent, Math.Round(spent / g.Count(), 2), g.Max(o => o.CreatedAt));
        }).OrderByDescending(r => r.Spent).ToList();
    }
}
