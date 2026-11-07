using System.Security.Cryptography;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ContainerService : IContainerService
{
    private static readonly string[] AnaquelStatuses =
        { "disponible", "activo", "pendiente_reposicion", "completo", "danado", "fuera_servicio" };
    private static readonly string[] MovementTypes = { "delivery", "return", "damaged", "adjustment" };

    private readonly AppDbContext _db;

    public ContainerService(AppDbContext db) => _db = db;

    // ── Tipos de envase ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ContainerTypeDto>> GetTypesAsync(Guid affiliateId, bool includeInactive)
    {
        var q = _db.ContainerTypes.AsNoTracking().Where(t => t.AffiliateId == affiliateId);
        if (!includeInactive) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ContainerTypeDto> CreateTypeAsync(Guid affiliateId, UpsertContainerTypeRequest request, string? actorId)
    {
        ValidateType(request);
        var type = new ContainerType { AffiliateId = affiliateId, CreatedBy = actorId };
        ApplyType(type, request);
        _db.ContainerTypes.Add(type);
        await _db.SaveChangesAsync();
        return ToDto(type);
    }

    public async Task<ContainerTypeDto?> UpdateTypeAsync(Guid affiliateId, Guid typeId, UpsertContainerTypeRequest request, string? actorId)
    {
        ValidateType(request);
        var type = await _db.ContainerTypes.FirstOrDefaultAsync(t => t.Id == typeId && t.AffiliateId == affiliateId);
        if (type is null) return null;
        ApplyType(type, request);
        type.UpdatedAt = DateTime.UtcNow;
        type.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return ToDto(type);
    }

    // ── Anaqueles ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AnaquelDto>> GetAnaquelesAsync(Guid affiliateId, Guid? customerId, string? status)
    {
        var q = _db.Anaqueles.AsNoTracking().Include(a => a.Customer).Where(a => a.AffiliateId == affiliateId);
        if (customerId.HasValue) q = q.Where(a => a.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        var list = await q.OrderBy(a => a.Label).ThenBy(a => a.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<AnaquelDto> CreateAnaquelAsync(Guid affiliateId, UpsertAnaquelRequest request, string? actorId)
    {
        var customer = await ValidateAnaquelAsync(affiliateId, request);
        var anaquel = new Anaquel
        {
            AffiliateId = affiliateId,
            QrCode = await NewQrCodeAsync(),
            CreatedBy = actorId,
        };
        ApplyAnaquel(anaquel, request, customer);
        _db.Anaqueles.Add(anaquel);
        await _db.SaveChangesAsync();
        anaquel.Customer = customer;
        return ToDto(anaquel);
    }

    public async Task<AnaquelDto?> UpdateAnaquelAsync(Guid affiliateId, Guid anaquelId, UpsertAnaquelRequest request, string? actorId)
    {
        var anaquel = await _db.Anaqueles.FirstOrDefaultAsync(a => a.Id == anaquelId && a.AffiliateId == affiliateId);
        if (anaquel is null) return null;
        var customer = await ValidateAnaquelAsync(affiliateId, request);
        ApplyAnaquel(anaquel, request, customer);
        anaquel.UpdatedAt = DateTime.UtcNow;
        anaquel.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        anaquel.Customer = customer;
        return ToDto(anaquel);
    }

    /// <summary>Resuelve el QR dentro del negocio activo: un código de otro negocio responde como inexistente.</summary>
    public async Task<AnaquelScanDto?> ResolveQrAsync(Guid affiliateId, string qrCode)
    {
        var code = (qrCode ?? string.Empty).Trim().ToUpperInvariant();
        var anaquel = await _db.Anaqueles.AsNoTracking().Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AffiliateId == affiliateId && a.QrCode == code);
        if (anaquel is null) return null;
        var balance = anaquel.CustomerId.HasValue ? await GetBalanceAsync(affiliateId, anaquel.CustomerId.Value) : null;
        return new AnaquelScanDto(ToDto(anaquel), balance);
    }

    // ── Movimientos y saldo ──────────────────────────────────────────────────

    public async Task<RegisterContainerMovementResultDto> RegisterMovementAsync(
        Guid affiliateId, RegisterContainerMovementRequest r, string? actorId, string? actorName)
    {
        if (!MovementTypes.Contains(r.Type))
            throw new ArgumentException("Tipo de movimiento inválido (delivery, return, damaged o adjustment).");
        if (r.Type == "adjustment" ? r.Quantity == 0 : r.Quantity <= 0)
            throw new ArgumentException(r.Type == "adjustment"
                ? "El ajuste no puede ser 0."
                : "La cantidad debe ser mayor que cero.");
        if (Math.Abs(r.Quantity) > 10_000)
            throw new ArgumentException("Cantidad fuera de rango.");

        var key = string.IsNullOrWhiteSpace(r.IdempotencyKey) ? null : r.IdempotencyKey.Trim();
        if (key is not null)
        {
            var existing = await _db.ContainerMovements.AsNoTracking().Include(m => m.ContainerType)
                .FirstOrDefaultAsync(m => m.AffiliateId == affiliateId && m.IdempotencyKey == key);
            if (existing is not null)
            {
                var bal = await GetBalanceAsync(affiliateId, existing.CustomerId)
                    ?? throw new InvalidOperationException("El cliente del movimiento ya no existe.");
                return new RegisterContainerMovementResultDto(ToDto(existing), true, bal);
            }
        }

        var containerType = await _db.ContainerTypes.FirstOrDefaultAsync(t => t.Id == r.ContainerTypeId && t.AffiliateId == affiliateId)
            ?? throw new ArgumentException("El tipo de envase no existe en este negocio.");
        if (!containerType.IsActive)
            throw new ArgumentException("El tipo de envase está desactivado.");

        Anaquel? anaquel = null;
        if (r.AnaquelId.HasValue)
            anaquel = await _db.Anaqueles.FirstOrDefaultAsync(a => a.Id == r.AnaquelId.Value && a.AffiliateId == affiliateId)
                ?? throw new ArgumentException("El anaquel no existe en este negocio.");

        var customerId = r.CustomerId ?? anaquel?.CustomerId
            ?? throw new ArgumentException("Falta el cliente (o un anaquel asignado a un cliente).");
        if (anaquel?.CustomerId is { } owner && owner != customerId)
            throw new ArgumentException("El anaquel pertenece a otro cliente.");
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.AffiliateId == affiliateId)
            ?? throw new ArgumentException("El cliente no existe en este negocio.");

        var movement = new ContainerMovement
        {
            AffiliateId = affiliateId,
            CustomerId = customer.Id,
            ContainerTypeId = containerType.Id,
            AnaquelId = anaquel?.Id,
            Type = r.Type,
            Quantity = r.Quantity,
            OccurredAt = NormalizeUtc(r.OccurredAt),
            RegisteredBy = actorName ?? actorId,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            IdempotencyKey = key,
        };
        _db.ContainerMovements.Add(movement);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (key is not null)
        {
            // Carrera: otro reintento con la misma clave ganó entre la lectura y el insert → es un duplicado.
            _db.Entry(movement).State = EntityState.Detached;
            var winner = await _db.ContainerMovements.AsNoTracking().Include(m => m.ContainerType)
                .FirstOrDefaultAsync(m => m.AffiliateId == affiliateId && m.IdempotencyKey == key);
            if (winner is null) throw;
            var bal = await GetBalanceAsync(affiliateId, winner.CustomerId)
                ?? throw new InvalidOperationException("El cliente del movimiento ya no existe.");
            return new RegisterContainerMovementResultDto(ToDto(winner), true, bal);
        }

        movement.ContainerType = containerType;
        var balance = await GetBalanceAsync(affiliateId, customer.Id)
            ?? throw new InvalidOperationException("El cliente del movimiento ya no existe.");
        return new RegisterContainerMovementResultDto(ToDto(movement), false, balance);
    }

    public async Task<CustomerContainerBalanceDto?> GetBalanceAsync(Guid affiliateId, Guid customerId)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.AffiliateId == affiliateId);
        if (customer is null) return null;

        var rows = await _db.ContainerMovements.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId && m.CustomerId == customerId)
            .GroupBy(m => new { m.ContainerTypeId, m.ContainerType!.Name })
            .Select(g => new
            {
                g.Key.ContainerTypeId,
                g.Key.Name,
                Delivered = g.Where(m => m.Type == "delivery").Sum(m => m.Quantity),
                Returned = g.Where(m => m.Type == "return").Sum(m => m.Quantity),
                Damaged = g.Where(m => m.Type == "damaged").Sum(m => m.Quantity),
                Adjustments = g.Where(m => m.Type == "adjustment").Sum(m => m.Quantity),
            })
            .ToListAsync();

        var items = rows
            .Select(x => new ContainerBalanceItemDto(
                x.ContainerTypeId, x.Name, x.Delivered, x.Returned, x.Damaged, x.Adjustments,
                x.Delivered - x.Returned - x.Damaged + x.Adjustments))
            .OrderBy(x => x.ContainerTypeName)
            .ToList();
        return new CustomerContainerBalanceDto(customer.Id, customer.Name, items);
    }

    public async Task<IReadOnlyList<ContainerMovementDto>> GetMovementsAsync(Guid affiliateId, Guid customerId, int take)
    {
        take = Math.Clamp(take, 1, 500);
        var list = await _db.ContainerMovements.AsNoTracking().Include(m => m.ContainerType)
            .Where(m => m.AffiliateId == affiliateId && m.CustomerId == customerId)
            .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void ValidateType(UpsertContainerTypeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100)
            throw new ArgumentException("El nombre del envase es obligatorio (máx. 100 caracteres).");
        if (r.CapacityLiters is < 0 or > 10_000)
            throw new ArgumentException("Capacidad en litros fuera de rango.");
        if (r.Deposit < 0)
            throw new ArgumentException("El depósito no puede ser negativo.");
    }

    private static void ApplyType(ContainerType t, UpsertContainerTypeRequest r)
    {
        t.Name = r.Name.Trim();
        t.CapacityLiters = r.CapacityLiters;
        t.Deposit = r.Deposit;
        t.IsActive = r.IsActive;
    }

    /// <summary>Valida el request y devuelve el cliente (o null si el anaquel queda sin asignar).</summary>
    private async Task<Customer?> ValidateAnaquelAsync(Guid affiliateId, UpsertAnaquelRequest r)
    {
        if (r.Capacity is < 0 or > 10_000)
            throw new ArgumentException("Capacidad del anaquel fuera de rango.");
        if (r.Status is not null && !AnaquelStatuses.Contains(r.Status))
            throw new ArgumentException("Estado de anaquel inválido.");
        if (r.Label is { Length: > 50 })
            throw new ArgumentException("La etiqueta no puede pasar de 50 caracteres.");
        if (r.Location is { Length: > 200 })
            throw new ArgumentException("La ubicación no puede pasar de 200 caracteres.");
        if (!r.CustomerId.HasValue) return null;
        return await _db.Customers.FirstOrDefaultAsync(c => c.Id == r.CustomerId.Value && c.AffiliateId == affiliateId)
            ?? throw new ArgumentException("El cliente no existe en este negocio.");
    }

    private static void ApplyAnaquel(Anaquel a, UpsertAnaquelRequest r, Customer? customer)
    {
        a.CustomerId = customer?.Id;
        a.Label = string.IsNullOrWhiteSpace(r.Label) ? null : r.Label.Trim();
        a.Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim();
        a.Capacity = r.Capacity;
        a.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        // Sin estado explícito: asignado a un cliente = activo; sin cliente = disponible.
        a.Status = r.Status ?? (customer is null ? "disponible" : "activo");
    }

    /// <summary>12 caracteres hex (48 bits aleatorios). El índice único de la BD cubre la colisión remota.</summary>
    private async Task<string> NewQrCodeAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            if (!await _db.Anaqueles.AnyAsync(a => a.QrCode == code)) return code;
        }
        throw new InvalidOperationException("No se pudo generar un código QR único; intenta de nuevo.");
    }

    private static DateTime NormalizeUtc(DateTime? value) => value switch
    {
        null => DateTime.UtcNow,
        { Kind: DateTimeKind.Unspecified } d => DateTime.SpecifyKind(d, DateTimeKind.Utc),
        { } d => d.ToUniversalTime(),
    };

    private static ContainerTypeDto ToDto(ContainerType t) => new(t.Id, t.Name, t.CapacityLiters, t.Deposit, t.IsActive);

    private static AnaquelDto ToDto(Anaquel a) =>
        new(a.Id, a.QrCode, a.Label, a.Location, a.Capacity, a.Status, a.CustomerId, a.Customer?.Name, a.Notes);

    private static ContainerMovementDto ToDto(ContainerMovement m) =>
        new(m.Id, m.CustomerId, m.ContainerTypeId, m.ContainerType?.Name, m.AnaquelId, m.Type, m.Quantity,
            m.OccurredAt, m.RegisteredBy, m.Notes);
}
