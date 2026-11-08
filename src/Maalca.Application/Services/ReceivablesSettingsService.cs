using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ReceivablesSettingsService : IReceivablesSettingsService
{
    private readonly AppDbContext _db;

    public ReceivablesSettingsService(AppDbContext db) => _db = db;

    /// <summary>Lectura compartida por los servicios de cobros y facturación: sin fila devuelve los valores por defecto.</summary>
    public static async Task<ReceivablesSettingsDto> LoadAsync(AppDbContext db, Guid affiliateId)
    {
        var s = await db.ReceivablesSettings.AsNoTracking().FirstOrDefaultAsync(x => x.AffiliateId == affiliateId);
        return s is null ? new ReceivablesSettingsDto() : ToDto(s);
    }

    public Task<ReceivablesSettingsDto> GetAsync(Guid affiliateId) => LoadAsync(_db, affiliateId);

    public async Task<ReceivablesSettingsDto> UpdateAsync(Guid affiliateId, ReceivablesSettingsDto request)
    {
        var basis = (request.AgingBasis ?? "").Trim().ToLowerInvariant();
        var mode = (request.TaxMode ?? "").Trim().ToLowerInvariant();
        var label = (request.TaxLabel ?? "").Trim();
        if (basis is not ("issue" or "due")) throw new ArgumentException("La antigüedad se cuenta desde la emisión (issue) o el vencimiento (due).");
        if (mode is not ("none" or "added" or "included")) throw new ArgumentException("Modo de impuesto inválido (none, added o included).");
        if (request.TaxRate is < 0 or > 30) throw new ArgumentException("La tasa debe estar entre 0 y 30%.");
        if (mode != "none" && request.TaxRate <= 0) throw new ArgumentException("Indica la tasa del impuesto (mayor que 0).");
        if (label.Length is 0 or > 20) throw new ArgumentException("El nombre del impuesto debe tener entre 1 y 20 caracteres.");

        var s = await _db.ReceivablesSettings.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId);
        if (s is null)
        {
            s = new ReceivablesSettings { AffiliateId = affiliateId };
            _db.ReceivablesSettings.Add(s);
        }
        else s.UpdatedAt = DateTime.UtcNow;
        s.AgingBasis = basis;
        s.TaxMode = mode;
        s.TaxRate = mode == "none" ? 0m : Math.Round(request.TaxRate, 3);
        s.TaxLabel = label;
        await _db.SaveChangesAsync();
        return ToDto(s);
    }

    private static ReceivablesSettingsDto ToDto(ReceivablesSettings s) => new(s.AgingBasis, s.TaxMode, s.TaxRate, s.TaxLabel);
}
