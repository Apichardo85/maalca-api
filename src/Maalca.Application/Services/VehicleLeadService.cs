using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Interesados de la vitrina Dealer (ver VehicleLead.cs). El alta pública valida contra la configuración del
/// dueño (Affiliate.Settings -> "dealer"): un formulario apagado no recibe leads aunque alguien llame al API
/// directo. La cuota del financiamiento la calcula este servicio -- el número del navegador se ignora.
/// </summary>
public class VehicleLeadService : IVehicleLeadService
{
    private static readonly string[] Kinds = { "financing", "tradein", "inquiry", "testdrive" };
    private static readonly string[] Statuses = { "new", "contacted", "won", "lost" };
    private static readonly string[] TradeConditions = { "excellent", "good", "fair", "poor" };

    private const int MaxTradeImages = 6;
    private const int MaxLeadsPerAffiliatePerHour = 40;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);
    private const int ListMax = 500;

    private readonly AppDbContext _db;

    public VehicleLeadService(AppDbContext db) => _db = db;

    // ── Alta pública ─────────────────────────────────────────────────────────────

    public async Task<PublicVehicleLeadResultDto> CreatePublicAsync(string slug, CreateVehicleLeadRequest r)
    {
        var affiliate = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Slug == slug && a.Published && a.BusinessType == BusinessType.Dealer)
            .Select(a => new { a.Id, a.Settings, a.Currency })
            .FirstOrDefaultAsync();
        if (affiliate is null) throw new KeyNotFoundException();

        var settings = VehicleListingService.ParseSettings(affiliate.Settings);
        var currency = string.Equals(affiliate.Currency, "DOP", StringComparison.OrdinalIgnoreCase) ? "DOP" : "USD";

        var kind = (r.Kind ?? "").Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind)) throw new ArgumentException("Tipo de solicitud no válido.");
        if (kind == "financing" && !settings.FinancingEnabled) throw new ArgumentException("Este negocio no ofrece financiamiento en línea.");
        if (kind == "tradein" && !settings.TradeInEnabled) throw new ArgumentException("Este negocio no recibe vehículos a cambio en línea.");
        if (kind == "testdrive" && !settings.TestDriveEnabled) throw new ArgumentException("Este negocio no agenda pruebas de manejo en línea.");

        // Contacto
        var name = Clip(r.Name, 100);
        if (name is null) throw new ArgumentException("El nombre es requerido.");
        var phone = Clip(r.Phone, 30);
        var email = Clip(r.Email, 200)?.ToLowerInvariant();
        if (phone is null && email is null) throw new ArgumentException("Deja un teléfono o un correo para poder contactarte.");
        if (phone is not null && !PhoneRules.IsValid(phone)) throw new ArgumentException("El teléfono no es válido: escribe 10 dígitos.");
        if (email is not null && (!email.Contains('@') || email.Contains(' '))) throw new ArgumentException("El correo no es válido.");
        var message = Clip(r.Message, 1000);

        // Vehículo (obligatorio para financiamiento y prueba de manejo)
        VehicleListing? vehicle = null;
        if (r.VehicleId is Guid vid)
        {
            vehicle = await _db.VehicleListings.AsNoTracking().FirstOrDefaultAsync(v =>
                v.Id == vid && v.AffiliateId == affiliate.Id && v.IsPubliclyVisible && v.Status != "sold");
            if (vehicle is null) throw new ArgumentException("Ese vehículo ya no está disponible.");
        }
        if ((kind == "financing" || kind == "testdrive") && vehicle is null)
            throw new ArgumentException("Elige el vehículo.");

        var lead = new VehicleLead
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliate.Id,
            VehicleListingId = vehicle?.Id,
            VehicleSummary = vehicle is null ? null : Summary(vehicle),
            Kind = kind,
            Name = name,
            Phone = phone,
            Email = email,
            Message = message,
            Status = "new",
            Source = "web",
            CreatedAt = DateTime.UtcNow,
        };

        if (kind == "financing")
        {
            var down = r.DownPayment ?? 0m;
            var term = r.TermMonths ?? 0;
            if (!settings.FinancingTerms!.Contains(term)) throw new ArgumentException("Ese plazo no está disponible.");
            if (down < 0m || down >= vehicle!.Price) throw new ArgumentException("El inicial no es válido.");
            // Tolerancia de 1 unidad: la web calcula el mínimo en la moneda de pantalla y redondea al convertir.
            if (down + 1m < Math.Round(vehicle.Price * settings.MinDownPercent / 100m, 2))
                throw new ArgumentException($"El inicial mínimo es {settings.MinDownPercent:0.##}% del precio.");

            lead.Currency = currency;
            lead.DownPayment = Math.Round(down, 2);
            lead.TermMonths = term;
            lead.MonthlyEstimate = MonthlyPayment(vehicle.Price - down, settings.FinancingRate ?? 0m, term);
        }
        else if (kind == "tradein")
        {
            var make = Clip(r.TradeMake, 60);
            var model = Clip(r.TradeModel, 80);
            if (make is null || model is null) throw new ArgumentException("Indica la marca y el modelo de tu vehículo.");
            if (r.TradeYear is null || r.TradeYear < 1950 || r.TradeYear > DateTime.UtcNow.Year + 1)
                throw new ArgumentException("El año de tu vehículo no es válido.");
            if (r.TradeMileage is < 0 or > 2_000_000) throw new ArgumentException("El millaje no es válido.");
            var tc = r.TradeCondition?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tc) && !TradeConditions.Contains(tc)) throw new ArgumentException("El estado de tu vehículo no es válido.");
            var mu = r.TradeMileageUnit?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(mu) && mu != "km" && mu != "mi") throw new ArgumentException("La unidad de millaje no es válida.");

            var images = (r.TradeImages ?? new List<string>())
                .Select(u => u?.Trim() ?? "")
                .Where(u => u.Length > 0 && u.Length <= 500 && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                .Distinct().Take(MaxTradeImages).ToList();

            lead.TradeMake = make;
            lead.TradeModel = model;
            lead.TradeYear = r.TradeYear;
            lead.TradeMileage = r.TradeMileage;
            lead.TradeMileageUnit = r.TradeMileage is null ? null : (mu is "mi" ? "mi" : "km");
            lead.TradeCondition = string.IsNullOrEmpty(tc) ? null : tc;
            lead.TradeImages = images.Count == 0 ? null : JsonArrayField.Serialize(images);
        }
        else if (kind == "testdrive" && r.PreferredAt is { } pref)
        {
            if (pref < DateTime.UtcNow.AddHours(-12) || pref > DateTime.UtcNow.AddDays(90))
                throw new ArgumentException("La fecha de la prueba no es válida.");
            lead.PreferredAt = pref.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(pref, DateTimeKind.Utc) : pref.ToUniversalTime();
        }

        // Doble envío: la misma persona pidiendo lo mismo del mismo vehículo en pocos minutos = el mismo lead.
        var since = DateTime.UtcNow - DuplicateWindow;
        var existing = await _db.VehicleLeads.AsNoTracking()
            .Where(l => l.AffiliateId == affiliate.Id && l.Kind == kind && l.VehicleListingId == lead.VehicleListingId
                        && l.CreatedAt >= since
                        && ((phone != null && l.Phone == phone) || (email != null && l.Email == email)))
            .Select(l => new { l.Id, l.MonthlyEstimate, l.Currency })
            .FirstOrDefaultAsync();
        if (existing is not null)
            return new PublicVehicleLeadResultDto(existing.Id, existing.MonthlyEstimate, existing.Currency);

        // Tope por hora y por negocio: freno barato contra spam sin montar infraestructura de rate limiting.
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        if (await _db.VehicleLeads.CountAsync(l => l.AffiliateId == affiliate.Id && l.CreatedAt >= hourAgo) >= MaxLeadsPerAffiliatePerHour)
            throw new InvalidOperationException("Demasiadas solicitudes en este momento. Inténtalo de nuevo más tarde.");

        _db.VehicleLeads.Add(lead);
        await _db.SaveChangesAsync();
        return new PublicVehicleLeadResultDto(lead.Id, lead.MonthlyEstimate, lead.Currency);
    }

    /// <summary>Cuota mensual de un préstamo amortizado (tasa anual en %). Tasa 0 = principal / meses.</summary>
    public static decimal MonthlyPayment(decimal principal, decimal annualRatePercent, int months)
    {
        if (months <= 0 || principal <= 0m) return 0m;
        if (annualRatePercent <= 0m) return Math.Round(principal / months, 2);
        var r = (double)annualRatePercent / 100d / 12d;
        var payment = (double)principal * r / (1d - Math.Pow(1d + r, -months));
        return Math.Round((decimal)payment, 2);
    }

    // ── Dashboard ────────────────────────────────────────────────────────────────

    public async Task<List<VehicleLeadDto>> GetAsync(Guid affiliateId, string? status = null)
    {
        var query = _db.VehicleLeads.AsNoTracking().Where(l => l.AffiliateId == affiliateId);
        var s = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(s) && Statuses.Contains(s)) query = query.Where(l => l.Status == s);
        var rows = await query.OrderByDescending(l => l.CreatedAt).Take(ListMax).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<VehicleLeadDto?> UpdateAsync(Guid affiliateId, Guid id, UpdateVehicleLeadRequest r)
    {
        var status = (r.Status ?? "").Trim().ToLowerInvariant();
        if (!Statuses.Contains(status)) throw new ArgumentException("Estado no válido.");
        var lead = await _db.VehicleLeads.FirstOrDefaultAsync(l => l.Id == id && l.AffiliateId == affiliateId);
        if (lead is null) return null;
        lead.Status = status;
        lead.InternalNotes = Clip(r.InternalNotes, 2000);
        lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(lead);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var lead = await _db.VehicleLeads.FirstOrDefaultAsync(l => l.Id == id && l.AffiliateId == affiliateId);
        if (lead is null) return false;
        _db.VehicleLeads.Remove(lead);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<int> CountNewAsync(Guid affiliateId) =>
        _db.VehicleLeads.CountAsync(l => l.AffiliateId == affiliateId && l.Status == "new");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string Summary(VehicleListing v) =>
        string.Join(' ', new[] { v.Year.ToString(), v.Make, v.Model, v.Trim }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string? Clip(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    private static VehicleLeadDto ToDto(VehicleLead l) => new(
        l.Id, l.VehicleListingId, l.VehicleSummary, l.Kind, l.Name, l.Phone, l.Email, l.Message,
        l.Currency, l.DownPayment, l.TermMonths, l.MonthlyEstimate,
        l.TradeMake, l.TradeModel, l.TradeYear, l.TradeMileage, l.TradeMileageUnit, l.TradeCondition,
        JsonArrayField.Parse<string>(l.TradeImages), l.PreferredAt, l.Status, l.InternalNotes, l.CreatedAt);
}
