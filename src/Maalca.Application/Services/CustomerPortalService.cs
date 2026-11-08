using System.Security.Cryptography;
using System.Text;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public interface ICustomerPortalService
{
    /// <summary>Null si la tienda no existe o la persona no es un cliente aprobado de ella.</summary>
    Task<CustomerPortalDto?> GetAsync(string slug, string supabaseUserId);
    Task<ContractDocumentDto?> GetContractDocumentAsync(string slug, string supabaseUserId);
    /// <summary>ArgumentException = dato inválido o contrato que cambió; null = no es cliente / no tiene ficha.</summary>
    Task<PortalContractDto?> AcceptContractAsync(string slug, string supabaseUserId, AcceptContractRequest request);
}

/// <summary>
/// Portal del cliente: une su cuenta de tienda (StoreCustomer, identidad de Supabase) con su ficha de Cliente (por correo, igual
/// que al aprobarlo) y devuelve contrato, envases, saldo, facturas, pagos, entregas y próxima visita. Solo lectura, salvo aceptar el contrato.
/// </summary>
public class CustomerPortalService : ICustomerPortalService
{
    private readonly AppDbContext _db;
    private readonly IContractService _contracts;
    private readonly IBillingService _billing;
    private readonly IContainerService _containers;
    private readonly IOwnerNotificationService _owner;

    public CustomerPortalService(AppDbContext db, IContractService contracts, IBillingService billing,
        IContainerService containers, IOwnerNotificationService owner)
    {
        _db = db;
        _contracts = contracts;
        _billing = billing;
        _containers = containers;
        _owner = owner;
    }

    private sealed record Who(Affiliate Affiliate, StoreCustomer Account, Customer? Customer);

    private async Task<Who?> ResolveAsync(string slug, string supabaseUserId)
    {
        if (string.IsNullOrWhiteSpace(supabaseUserId)) return null;
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.Published);
        if (affiliate is null) return null;
        var account = await _db.StoreCustomers.AsNoTracking().FirstOrDefaultAsync(c =>
            c.AffiliateId == affiliate.Id && c.SupabaseUserId == supabaseUserId && c.Status == StoreAccounts.Approved);
        if (account is null) return null;
        var email = account.Email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c =>
            c.AffiliateId == affiliate.Id && c.Email != null && c.Email.ToLower() == email);
        return new Who(affiliate, account, customer);
    }

    private static string HashOf(ContractDocumentDto d) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(d.Title + "\n" + d.Body))).ToLowerInvariant();

    private static PortalContractDto ToPortal(Contract c, string currentHash)
    {
        var changed = c.AcceptedAt != null && !string.Equals(c.AcceptedDocumentHash, currentHash, StringComparison.Ordinal);
        return new PortalContractDto(
            c.Id, c.ContractNumber, c.DeliveryFrequency, c.BillingCycle, c.CreditDays, c.StartDate, c.EndDate,
            c.RacksLoaned, c.BottlesLoaned,
            Accepted: c.AcceptedAt != null && !changed, c.AcceptedAt, c.AcceptedByName, changed, currentHash);
    }

    private Task<Contract?> ActiveContractAsync(Guid affiliateId, Guid customerId, bool track)
    {
        var q = _db.Contracts.Where(c => c.AffiliateId == affiliateId && c.CustomerId == customerId && c.IsActive);
        if (!track) q = q.AsNoTracking();
        return q.OrderByDescending(c => c.StartDate).FirstOrDefaultAsync();
    }

    public async Task<CustomerPortalDto?> GetAsync(string slug, string supabaseUserId)
    {
        var who = await ResolveAsync(slug, supabaseUserId);
        if (who is null) return null;
        var customer = who.Customer;
        if (customer is null)
        {
            return new CustomerPortalDto(false, who.Account.Name, who.Account.Address, null,
                Array.Empty<ContainerBalanceItemDto>(), Array.Empty<PortalAnaquelDto>(),
                0m, 0m, Array.Empty<OpenInvoiceDto>(), Array.Empty<PortalPaymentDto>(), Array.Empty<PortalDeliveryDto>(),
                null, who.Account.DeliveryFrequency);
        }

        var affiliateId = who.Affiliate.Id;

        PortalContractDto? contractDto = null;
        var contract = await ActiveContractAsync(affiliateId, customer.Id, track: false);
        if (contract is not null)
        {
            var doc = await _contracts.RenderDocumentAsync(affiliateId, contract.Id);
            contractDto = ToPortal(contract, doc is null ? string.Empty : HashOf(doc));
        }

        var balance = await _containers.GetBalanceAsync(affiliateId, customer.Id);
        var containers = (balance?.Items ?? Array.Empty<ContainerBalanceItemDto>())
            .Where(i => i.Delivered > 0 || i.Outstanding != 0).ToList();

        var anaqueles = (await _db.Anaqueles.AsNoTracking()
                .Where(a => a.AffiliateId == affiliateId && a.CustomerId == customer.Id && a.Status != "fuera_servicio")
                .OrderBy(a => a.Label).ToListAsync())
            .Select(a => new PortalAnaquelDto(a.Label, a.Location, a.Status)).ToList();

        var statement = await _billing.GetStatementAsync(affiliateId, customer.Id);

        var payments = (await _db.Payments.AsNoTracking()
                .Where(p => p.AffiliateId == affiliateId && p.CustomerId == customer.Id)
                .OrderByDescending(p => p.PaidAt).Take(5).ToListAsync())
            .Select(p => new PortalPaymentDto(p.PaidAt, p.Amount, p.Method)).ToList();

        var deliveries = (await _db.Deliveries.AsNoTracking().Include(d => d.Items)
                .Where(d => d.AffiliateId == affiliateId && d.CustomerId == customer.Id)
                .OrderByDescending(d => d.DeliveredAt).Take(5).ToListAsync())
            .Select(d =>
            {
                var items = d.Items.Select(i => new DeliveryItemDto(i.ItemId, i.Description, i.Quantity, i.UnitPrice, Math.Round(i.Quantity * i.UnitPrice, 2))).ToList();
                return new PortalDeliveryDto(d.DeliveredAt, items, items.Sum(i => i.LineTotal));
            }).ToList();

        // Próxima visita: primera parada pendiente en una ruta de hoy (hora de RD, UTC-4) en adelante.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
        var next = await (from s in _db.RouteStops.AsNoTracking()
                          join r in _db.DeliveryRoutes.AsNoTracking() on s.RouteId equals r.Id
                          where r.AffiliateId == affiliateId && s.CustomerId == customer.Id && s.Status == "pending"
                                && r.RouteDate >= today && (r.Status == "planned" || r.Status == "loading" || r.Status == "in_progress")
                          orderby r.RouteDate
                          select new { s.RouteId, s.SortOrder, r.RouteDate, r.Status }).FirstOrDefaultAsync();
        PortalNextVisitDto? nextVisit = null;
        if (next is not null)
        {
            var onTheWay = next.Status == "in_progress";
            int? before = onTheWay
                ? await _db.RouteStops.AsNoTracking().CountAsync(s => s.RouteId == next.RouteId && s.Status == "pending" && s.SortOrder < next.SortOrder)
                : null;
            nextVisit = new PortalNextVisitDto(next.RouteDate, next.Status, onTheWay, before);
        }

        return new CustomerPortalDto(
            true, customer.Name, customer.Address, contractDto, containers, anaqueles,
            statement?.Balance ?? 0m, statement?.CreditOnAccount ?? 0m,
            statement?.OpenInvoices ?? Array.Empty<OpenInvoiceDto>(),
            payments, deliveries, nextVisit,
            contract?.DeliveryFrequency ?? customer.DeliveryFrequency);
    }

    public async Task<ContractDocumentDto?> GetContractDocumentAsync(string slug, string supabaseUserId)
    {
        var who = await ResolveAsync(slug, supabaseUserId);
        if (who?.Customer is null) return null;
        var contract = await ActiveContractAsync(who.Affiliate.Id, who.Customer.Id, track: false);
        return contract is null ? null : await _contracts.RenderDocumentAsync(who.Affiliate.Id, contract.Id);
    }

    public async Task<PortalContractDto?> AcceptContractAsync(string slug, string supabaseUserId, AcceptContractRequest r)
    {
        var who = await ResolveAsync(slug, supabaseUserId);
        if (who?.Customer is null) return null;

        var name = r.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3) throw new ArgumentException("Escribe tu nombre completo para firmar.");
        if (name.Length > 160) name = name[..160];
        if (!r.Agree) throw new ArgumentException("Marca que aceptas el contrato.");

        var contract = await ActiveContractAsync(who.Affiliate.Id, who.Customer.Id, track: true);
        if (contract is null) throw new ArgumentException("No tienes un contrato activo para aceptar.");

        var doc = await _contracts.RenderDocumentAsync(who.Affiliate.Id, contract.Id)
                  ?? throw new ArgumentException("No se pudo preparar el contrato. Intenta de nuevo.");
        var hash = HashOf(doc);
        if (!string.Equals(r.DocumentHash, hash, StringComparison.Ordinal))
            throw new ArgumentException("El contrato cambió mientras lo leías. Recarga la página y revísalo otra vez.");

        // Ya aceptado y sin cambios: no se vuelve a registrar (conserva la fecha original).
        if (contract.AcceptedAt != null && contract.AcceptedDocumentHash == hash) return ToPortal(contract, hash);

        contract.AcceptedAt = DateTime.UtcNow;
        contract.AcceptedByName = name;
        contract.AcceptedByEmail = who.Account.Email;
        contract.AcceptedByStoreCustomerId = who.Account.Id;
        contract.AcceptedDocumentHash = hash;
        contract.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var folio = string.IsNullOrWhiteSpace(contract.ContractNumber) ? string.Empty : $" #{contract.ContractNumber}";
        await _owner.NotifyAsync(who.Affiliate.Id, "contract_accepted",
            "Contrato aceptado en línea", $"{who.Customer.Name}{folio}",
            "Contract accepted online", $"{who.Customer.Name}{folio}",
            "contratos", contract.Id);

        return ToPortal(contract, hash);
    }
}
