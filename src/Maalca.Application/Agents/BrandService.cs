using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Agents;

// ---------- DTOs de /ops/negocios/{id}/marca ----------

public record BrandProfileDto(Guid AffiliateId, string? Voice, string? Audience, string? Offer, string? Dos,
    string? Donts, string? SpecialDates, string? Hashtags, string Language, string? Notes, DateTime? UpdatedAt, string? UpdatedBy);

public record UpsertBrandProfileRequest(string? Voice, string? Audience, string? Offer, string? Dos, string? Donts,
    string? SpecialDates, string? Hashtags, string? Language, string? Notes);

public record AgentAffiliatePlanDto(Guid AffiliateId, int PostsPerMonth, List<string> Networks, bool AssistantEnabled,
    long MonthlyTokenCap, long TokensUsedThisMonth, DateTime? UpdatedAt, string? UpdatedBy);

public record UpsertAgentAffiliatePlanRequest(int PostsPerMonth, List<string>? Networks, bool AssistantEnabled, long MonthlyTokenCap);

public interface IBrandService
{
    Task<BrandProfileDto> GetBrandAsync(Guid affiliateId);
    Task<BrandProfileDto> UpsertBrandAsync(Guid affiliateId, UpsertBrandProfileRequest request, string? by);
    Task<AgentAffiliatePlanDto> GetPlanAsync(Guid affiliateId);
    Task<AgentAffiliatePlanDto> UpsertPlanAsync(Guid affiliateId, UpsertAgentAffiliatePlanRequest request, string? by);
}

/// <summary>
/// Perfil de marca y plan de agentes de un negocio. Las validaciones lanzan ArgumentException (→ 400 INVALID_INPUT);
/// un negocio que no existe lanza KeyNotFoundException (→ 404).
/// </summary>
public class BrandService : IBrandService
{
    public static readonly string[] AllowedNetworks = { "facebook", "instagram", "tiktok", "youtube" };
    private static readonly string[] AllowedLanguages = { "es", "en", "es,en" };

    private readonly AppDbContext _db;

    public BrandService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BrandProfileDto> GetBrandAsync(Guid affiliateId)
    {
        await EnsureAffiliateAsync(affiliateId);
        var p = await _db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(b => b.AffiliateId == affiliateId);
        return p is null
            ? new BrandProfileDto(affiliateId, null, null, null, null, null, null, null, "es", null, null, null)
            : ToDto(p);
    }

    public async Task<BrandProfileDto> UpsertBrandAsync(Guid affiliateId, UpsertBrandProfileRequest request, string? by)
    {
        await EnsureAffiliateAsync(affiliateId);

        var language = string.IsNullOrWhiteSpace(request.Language) ? "es" : request.Language.Trim().ToLowerInvariant().Replace(" ", "");
        if (!AllowedLanguages.Contains(language))
            throw new ArgumentException("El idioma debe ser \"es\", \"en\" o \"es,en\".");

        var entity = await _db.BrandProfiles.FirstOrDefaultAsync(b => b.AffiliateId == affiliateId);
        if (entity is null)
        {
            entity = new BrandProfile { AffiliateId = affiliateId, CreatedBy = by };
            _db.BrandProfiles.Add(entity);
        }

        entity.Voice = Clean(request.Voice, "La voz", 2000);
        entity.Audience = Clean(request.Audience, "El público", 2000);
        entity.Offer = Clean(request.Offer, "La oferta", 2000);
        entity.Dos = Clean(request.Dos, "Lo que sí", 2000);
        entity.Donts = Clean(request.Donts, "Lo que nunca", 2000);
        entity.SpecialDates = Clean(request.SpecialDates, "Las fechas especiales", 4000);
        entity.Hashtags = Clean(request.Hashtags, "Los hashtags", 1000);
        entity.Notes = Clean(request.Notes, "Las notas", 4000);
        entity.Language = language;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;

        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<AgentAffiliatePlanDto> GetPlanAsync(Guid affiliateId)
    {
        await EnsureAffiliateAsync(affiliateId);
        var p = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(x => x.AffiliateId == affiliateId);
        var used = await TokensUsedThisMonthAsync(affiliateId);
        return p is null
            ? new AgentAffiliatePlanDto(affiliateId, 0, new List<string>(), false, 0, used, null, null)
            : ToDto(p, used);
    }

    public async Task<AgentAffiliatePlanDto> UpsertPlanAsync(Guid affiliateId, UpsertAgentAffiliatePlanRequest request, string? by)
    {
        await EnsureAffiliateAsync(affiliateId);

        if (request.PostsPerMonth < 0 || request.PostsPerMonth > 200)
            throw new ArgumentException("Las publicaciones por mes deben estar entre 0 y 200.");
        if (request.MonthlyTokenCap < 0 || request.MonthlyTokenCap > 50_000_000)
            throw new ArgumentException("El tope mensual de tokens debe estar entre 0 y 50 000 000.");
        if (request.AssistantEnabled && request.MonthlyTokenCap == 0)
            throw new ArgumentException("Para activar el asistente hay que fijar un tope mensual de tokens mayor que 0.");

        var networks = new List<string>();
        foreach (var raw in request.Networks ?? new List<string>())
        {
            var n = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Length == 0) continue;
            if (!AllowedNetworks.Contains(n)) throw new ArgumentException($"La red \"{n}\" no existe.");
            if (!networks.Contains(n)) networks.Add(n);
        }

        var entity = await _db.AgentAffiliatePlans.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId);
        if (entity is null)
        {
            entity = new AgentAffiliatePlan { AffiliateId = affiliateId, CreatedBy = by };
            _db.AgentAffiliatePlans.Add(entity);
        }

        entity.PostsPerMonth = request.PostsPerMonth;
        entity.Networks = string.Join(',', networks);
        entity.AssistantEnabled = request.AssistantEnabled;
        entity.MonthlyTokenCap = request.MonthlyTokenCap;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;

        await _db.SaveChangesAsync();
        return ToDto(entity, await TokensUsedThisMonthAsync(affiliateId));
    }

    private async Task EnsureAffiliateAsync(Guid affiliateId)
    {
        if (!await _db.Affiliates.AnyAsync(a => a.Id == affiliateId))
            throw new KeyNotFoundException("Ese negocio no existe.");
    }

    private async Task<long> TokensUsedThisMonthAsync(Guid affiliateId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await AgentQuota.TokensUsedSinceAsync(_db, affiliateId, monthStart);
    }

    private static string? Clean(string? value, string label, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new ArgumentException($"{label} no puede pasar de {max} caracteres.");
        return v;
    }

    private static BrandProfileDto ToDto(BrandProfile p) => new(p.AffiliateId, p.Voice, p.Audience, p.Offer, p.Dos,
        p.Donts, p.SpecialDates, p.Hashtags, p.Language, p.Notes, p.UpdatedAt, p.UpdatedBy);

    private static AgentAffiliatePlanDto ToDto(AgentAffiliatePlan p, long used) => new(p.AffiliateId, p.PostsPerMonth,
        p.Networks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        p.AssistantEnabled, p.MonthlyTokenCap, used, p.UpdatedAt, p.UpdatedBy);
}

/// <summary>Consumo de tokens por negocio. Lo usan el runner (para cortar al llegar al tope) y el panel.</summary>
public static class AgentQuota
{
    public static async Task<long> TokensUsedSinceAsync(AppDbContext db, Guid affiliateId, DateTime since)
    {
        var rows = await db.AgentJobs.AsNoTracking()
            .Where(j => j.AffiliateId == affiliateId && j.CreatedAt >= since)
            .Select(j => new { j.TokensInput, j.TokensOutput })
            .ToListAsync();
        return rows.Sum(r => (long)r.TokensInput + r.TokensOutput);
    }
}
