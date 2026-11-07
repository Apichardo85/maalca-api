using System.Text.Json;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ContractService : IContractService
{
    private static readonly string[] DeliveryFrequencies = { "daily", "weekly", "on_demand" };
    private static readonly string[] BillingCycles = { "weekly", "biweekly", "monthly" };

    private readonly AppDbContext _db;

    public ContractService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContractDto>> GetAllAsync(Guid affiliateId, Guid? customerId, bool? active)
    {
        var q = _db.Contracts.AsNoTracking().Include(c => c.Customer).Where(c => c.AffiliateId == affiliateId);
        if (customerId.HasValue) q = q.Where(c => c.CustomerId == customerId.Value);
        if (active.HasValue) q = q.Where(c => c.IsActive == active.Value);
        var list = await q.OrderByDescending(c => c.StartDate).ThenByDescending(c => c.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ContractDto?> GetAsync(Guid affiliateId, Guid contractId)
    {
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.AffiliateId == affiliateId);
        return c is null ? null : ToDto(c);
    }

    public async Task<ContractDto> CreateAsync(Guid affiliateId, UpsertContractRequest request, string? actorId)
    {
        var agreed = Validate(request);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.AffiliateId == affiliateId)
            ?? throw new ArgumentException("El cliente no existe en este negocio.");
        if (request.IsActive) await EnsureNoOtherActiveAsync(affiliateId, request.CustomerId, null);

        var contract = new Contract
        {
            AffiliateId = affiliateId,
            CustomerId = request.CustomerId,
            CreatedBy = actorId,
        };
        Apply(contract, request, agreed);
        _db.Contracts.Add(contract);
        if (contract.IsActive) SyncCustomer(customer, contract);
        await _db.SaveChangesAsync();

        contract.Customer = customer;
        return ToDto(contract);
    }

    public async Task<ContractDto?> UpdateAsync(Guid affiliateId, Guid contractId, UpsertContractRequest request, string? actorId)
    {
        var agreed = Validate(request);
        var contract = await _db.Contracts.Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.AffiliateId == affiliateId);
        if (contract is null) return null;
        if (request.CustomerId != contract.CustomerId)
            throw new ArgumentException("No se puede cambiar el cliente de un contrato; crea uno nuevo.");
        if (request.IsActive) await EnsureNoOtherActiveAsync(affiliateId, contract.CustomerId, contract.Id);

        Apply(contract, request, agreed);
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = actorId;
        if (contract.IsActive && contract.Customer is not null) SyncCustomer(contract.Customer, contract);
        await _db.SaveChangesAsync();
        return ToDto(contract);
    }

    public async Task<ContractDto?> DeactivateAsync(Guid affiliateId, Guid contractId, string? actorId)
    {
        var contract = await _db.Contracts.Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.AffiliateId == affiliateId);
        if (contract is null) return null;
        if (contract.IsActive)
        {
            contract.IsActive = false;
            contract.EndDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = actorId;
            await _db.SaveChangesAsync();
        }
        return ToDto(contract);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Valida el request y devuelve el JSON de precios acordados listo para guardar (o null).</summary>
    private static string? Validate(UpsertContractRequest r)
    {
        if (!DeliveryFrequencies.Contains(r.DeliveryFrequency))
            throw new ArgumentException("Frecuencia de entrega inválida (daily, weekly u on_demand).");
        if (!BillingCycles.Contains(r.BillingCycle))
            throw new ArgumentException("Ciclo de cobro inválido (weekly, biweekly o monthly).");
        if (r.CreditDays is < 0 or > 365)
            throw new ArgumentException("Los días de crédito deben estar entre 0 y 365.");
        if (r.EndDate.HasValue && r.EndDate.Value < r.StartDate)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");

        if (r.AgreedPrices is { } ap && ap.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            if (ap.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Los precios acordados deben ser una lista [{itemId, price}].");
            foreach (var row in ap.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty("itemId", out var item) || !Guid.TryParse(item.GetString(), out _)
                    || !row.TryGetProperty("price", out var price) || price.ValueKind != JsonValueKind.Number
                    || !price.TryGetDecimal(out var p) || p < 0)
                    throw new ArgumentException("Cada precio acordado necesita itemId (GUID) y price (número ≥ 0).");
            }
            return ap.GetRawText();
        }
        return null;
    }

    private async Task EnsureNoOtherActiveAsync(Guid affiliateId, Guid customerId, Guid? exceptContractId)
    {
        var exists = await _db.Contracts.AnyAsync(c =>
            c.AffiliateId == affiliateId && c.CustomerId == customerId && c.IsActive && c.Id != exceptContractId);
        if (exists)
            throw new InvalidOperationException("Este cliente ya tiene un contrato activo. Desactívalo antes de crear o activar otro.");
    }

    private static void Apply(Contract c, UpsertContractRequest r, string? agreedPricesJson)
    {
        c.DeliveryFrequency = r.DeliveryFrequency;
        c.BillingCycle = r.BillingCycle;
        c.CreditDays = r.CreditDays;
        c.AgreedPrices = agreedPricesJson;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.IsActive = r.IsActive;
        c.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }

    /// <summary>La hoja de reparto y los cobros leen estos campos del cliente: se mantienen iguales al contrato activo.</summary>
    private static void SyncCustomer(Customer customer, Contract c)
    {
        customer.DeliveryFrequency = c.DeliveryFrequency;
        customer.BillingCycle = c.BillingCycle;
        customer.CreditDays = c.CreditDays;
        customer.UpdatedAt = DateTime.UtcNow;
    }

    private static ContractDto ToDto(Contract c) => new(
        c.Id, c.CustomerId, c.Customer?.Name, c.DeliveryFrequency, c.BillingCycle, c.CreditDays,
        string.IsNullOrWhiteSpace(c.AgreedPrices) ? null : JsonSerializer.Deserialize<JsonElement>(c.AgreedPrices),
        c.StartDate, c.EndDate, c.IsActive, c.Notes, c.CreatedAt);
}
